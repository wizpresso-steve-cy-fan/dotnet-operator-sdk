﻿using FluentAssertions;

using k8s.LeaderElection;
using k8s.Models;

using KubeOps.Abstractions.Builder;
using KubeOps.Abstractions.Controller;
using KubeOps.Abstractions.Entities;
using KubeOps.Abstractions.Events;
using KubeOps.Abstractions.Finalizer;
using KubeOps.Abstractions.Queue;
using KubeOps.KubernetesClient;
using KubeOps.Operator.Builder;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Queue;
using KubeOps.Operator.Test.TestEntities;
using KubeOps.Operator.Watcher;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KubeOps.Operator.Test.Builder;

public class OperatorBuilderTest
{
    private readonly IOperatorBuilder _builder = new OperatorBuilder(new ServiceCollection(), new());

    [Fact]
    public void Should_Add_Default_Resources()
    {
        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(OperatorSettings) &&
            s.Lifetime == ServiceLifetime.Singleton);
        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(IKubernetesClient<Corev1Event>) &&
            s.Lifetime == ServiceLifetime.Transient);
        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(EventPublisher) &&
            s.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void Should_Add_Entity_Resources()
    {
        _builder.AddEntity<V1IntegrationTestEntity>(new EntityMetadata("test", "v1", "testentities"));

        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(IKubernetesClient<V1IntegrationTestEntity>) &&
            s.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void Should_Add_Controller_Resources()
    {
        _builder.AddController<TestController, V1IntegrationTestEntity>();

        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(IEntityController<V1IntegrationTestEntity>) &&
            s.ImplementationType == typeof(TestController) &&
            s.Lifetime == ServiceLifetime.Scoped);
        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(IHostedService) &&
            s.ImplementationType == typeof(ResourceWatcher<V1IntegrationTestEntity>) &&
            s.Lifetime == ServiceLifetime.Singleton);
        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(TimedEntityQueue<V1IntegrationTestEntity>) &&
            s.Lifetime == ServiceLifetime.Singleton);
        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(EntityRequeue<V1IntegrationTestEntity>) &&
            s.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void Should_Add_Finalizer_Resources()
    {
        _builder.AddFinalizer<TestFinalizer, V1IntegrationTestEntity>(string.Empty);

        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(TestFinalizer) &&
            s.Lifetime == ServiceLifetime.Transient);
        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(FinalizerRegistration) &&
            s.Lifetime == ServiceLifetime.Singleton);
        _builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(EntityFinalizerAttacher<TestFinalizer, V1IntegrationTestEntity>) &&
            s.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void Should_Add_Leader_Elector()
    {
        var builder = new OperatorBuilder(new ServiceCollection(), new() { EnableLeaderElection = true });
        builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(k8s.LeaderElection.LeaderElector) &&
            s.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Should_Add_LeaderAwareResourceWatcher()
    {
        var builder = new OperatorBuilder(new ServiceCollection(), new() { EnableLeaderElection = true });
        builder.AddController<TestController, V1IntegrationTestEntity>();

        builder.Services.Should().Contain(s =>
            s.ServiceType == typeof(IHostedService) &&
            s.ImplementationType == typeof(LeaderAwareResourceWatcher<V1IntegrationTestEntity>) &&
            s.Lifetime == ServiceLifetime.Singleton);
        builder.Services.Should().NotContain(s =>
            s.ServiceType == typeof(IHostedService) &&
            s.ImplementationType == typeof(ResourceWatcher<V1IntegrationTestEntity>) &&
            s.Lifetime == ServiceLifetime.Singleton);
    }

    private class TestController : IEntityController<V1IntegrationTestEntity>
    {
    }

    private class TestFinalizer : IEntityFinalizer<V1IntegrationTestEntity>
    {
    }
}