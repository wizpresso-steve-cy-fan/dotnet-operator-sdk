﻿using FluentAssertions;

using KubeOps.Abstractions.Controller;
using KubeOps.Abstractions.Queue;
using KubeOps.KubernetesClient;
using KubeOps.Operator.Queue;
using KubeOps.Operator.Test.TestEntities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KubeOps.Operator.Test.Controller;

public class CancelEntityRequeueIntegrationTest : IntegrationTestBase
{
    private readonly InvocationCounter<V1OperatorIntegrationTestEntity> _mock = new();
    private readonly IKubernetesClient _client = new KubernetesClient.KubernetesClient();
    private readonly TestNamespaceProvider _ns = new();

    [Fact]
    public async Task Should_Cancel_Requeue_If_New_Event_Fires()
    {
        // This test fires the reconcile, which in turn requeues the entity.
        // then immediately fires a new event, which should cancel the requeue.

        _mock.TargetInvocationCount = 2;
        var e = await _client.CreateAsync(
            new V1OperatorIntegrationTestEntity("test-entity", "username", _ns.Namespace));
        e.Spec.Username = "changed";
        await _client.UpdateAsync(e);
        await _mock.WaitForInvocations;

        _mock.Invocations.Count.Should().Be(2);
        Services.GetRequiredService<TimedEntityQueue<V1OperatorIntegrationTestEntity>>().Count.Should().Be(0);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await _ns.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _ns.DisposeAsync();
        _client.Dispose();
    }

    protected override void ConfigureHost(HostApplicationBuilder builder)
    {
        builder.Services
            .AddSingleton(_mock)
            .AddKubernetesOperator(s => s.Namespace = _ns.Namespace)
            .AddController<TestController, V1OperatorIntegrationTestEntity>();
    }

    private class TestController : IEntityController<V1OperatorIntegrationTestEntity>
    {
        private readonly InvocationCounter<V1OperatorIntegrationTestEntity> _svc;
        private readonly EntityRequeue<V1OperatorIntegrationTestEntity> _requeue;

        public TestController(
            InvocationCounter<V1OperatorIntegrationTestEntity> svc,
            EntityRequeue<V1OperatorIntegrationTestEntity> requeue)
        {
            _svc = svc;
            _requeue = requeue;
        }

        public Task ReconcileAsync(V1OperatorIntegrationTestEntity entity)
        {
            _svc.Invocation(entity);
            if (_svc.Invocations.Count < 2)
            {
                _requeue(entity, TimeSpan.FromMilliseconds(1000));
            }

            return Task.CompletedTask;
        }
    }
}
