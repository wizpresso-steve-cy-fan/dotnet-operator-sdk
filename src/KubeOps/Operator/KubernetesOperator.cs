﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using k8s;
using KubeOps.Operator.Client;
using KubeOps.Operator.Commands;
using KubeOps.Operator.DependencyInjection;
using KubeOps.Operator.Logging;
using KubeOps.Operator.Serialization;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace KubeOps.Operator
{
    public sealed class KubernetesOperator
    {
        internal const string NoStructuredLogs = "--no-structured-logs";
        private const string DefaultOperatorName = "KubernetesOperator";
        private readonly OperatorSettings _operatorSettings;

        private readonly IHostBuilder _builder = Host
            .CreateDefaultBuilder()
            .UseConsoleLifetime();

        public KubernetesOperator(string? operatorName = null)
        {
            operatorName ??= (operatorName
                              ?? Assembly.GetEntryAssembly()?.GetName().Name
                              ?? DefaultOperatorName).ToLowerInvariant();
            _operatorSettings = new OperatorSettings
            {
                Name = operatorName,
            };
        }

        public KubernetesOperator(OperatorSettings settings)
        {
            _operatorSettings = settings;
        }

        public Task<int> Run(string[] args)
        {
            ConfigureRequiredServices();

            var app = new CommandLineApplication<RunOperator>();

            _builder.ConfigureLogging(builder =>
            {
                builder.ClearProviders();
#if DEBUG
                builder.AddConsole(options => options.TimestampFormat = @"[hh:mm:ss] ");
#else
                if (args.Contains(NoStructuredLogs))
                {
                    builder.AddConsole(options =>
                    {
                        options.TimestampFormat = @"[dd.MM.yyyy - hh:mm:ss] ";
                        options.DisableColors = true;
                    });
                }
                else
                {
                    builder.AddStructuredConsole();
                }
#endif
            });

            var host = _builder.Build();

            app
                .Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(host.Services);

            DependencyInjector.Services = host.Services;
            JsonConvert.DefaultSettings = () => host.Services.GetRequiredService<JsonSerializerSettings>();

            return app.ExecuteAsync(args);
        }

        public KubernetesOperator ConfigureServices(Action<IServiceCollection> configuration)
        {
            _builder.ConfigureServices(configuration);
            return this;
        }

        private void ConfigureRequiredServices() =>
            _builder.ConfigureServices(services =>
            {
                services.AddSingleton(_operatorSettings);

                services.AddTransient(
                    _ => new JsonSerializerSettings
                    {
                        ContractResolver = new NamingConvention(),
                        Converters = new List<JsonConverter>
                            {new StringEnumConverter {NamingStrategy = new CamelCaseNamingStrategy()}},
                    });
                services.AddTransient(
                    _ => new SerializerBuilder()
                        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                        .WithNamingConvention(new NamingConvention())
                        .Build());

                services.AddTransient<EntitySerializer>();

                services.AddTransient<IKubernetesClient, KubernetesClient>();
                services.AddSingleton<IKubernetes>(
                    _ =>
                    {
                        var config = KubernetesClientConfiguration.BuildDefaultConfig();

                        return new Kubernetes(config, new ClientUrlFixer())
                        {
                            SerializationSettings =
                            {
                                ContractResolver = new NamingConvention(),
                                Converters = new List<JsonConverter>
                                    {new StringEnumConverter {NamingStrategy = new CamelCaseNamingStrategy()}}
                            },
                            DeserializationSettings =
                            {
                                ContractResolver = new NamingConvention(),
                                Converters = new List<JsonConverter>
                                    {new StringEnumConverter {NamingStrategy = new CamelCaseNamingStrategy()}}
                            }
                        };
                    });
            });
    }
}