namespace MastodonAwsServices

module Ecs =

    open Pulumi
    open Pulumi.FSharp
    open Pulumi.Aws.Acm
    open Pulumi.Aws.Ecs
    open Pulumi.Aws.Ecs.Inputs
    open Pulumi.Aws.Iam
    open Pulumi.Aws.LB
    open Pulumi.Aws.LB.Inputs
    open MastodonAwsServices.Ec2
    open MastodonAwsServices.Config.Values

    let createEcs () =

        (*
----------------------------------------
ECS Cluster
----------------------------------------
*)  
        let clusterArgs = ClusterArgs(
            CapacityProviders = inputList [input "FARGATE_SPOT"]
        )
        
        let cluster =
            Cluster(prefixMastodonResource "ecs-cluster", clusterArgs)

        (*
----------------------------------------
Certificate
----------------------------------------
*)
        let cert =
            let getCertificateInvokeArgs =
                GetCertificateInvokeArgs(
                    Domain = localDomain,
                    MostRecent = true,
                    Types = inputList [ input "AMAZON_ISSUED" ]
            )
            GetCertificate.Invoke(getCertificateInvokeArgs)

        (*
----------------------------------------
Application Load Balancer, Target Groups
and Listener and Listene rules
----------------------------------------
*)
        let loadBalancerArgs = LoadBalancerArgs(
            IpAddressType = "ipv4",
            LoadBalancerType = "application",
            SecurityGroups = inputList [ io loadBalancerSecurityGroup.Id],
            Subnets = inputList (defaultSubnetIds |> List.map io)
        )

        let loadBalancer = LoadBalancer(prefixMastodonResource "load-balancer", loadBalancerArgs)

        let webTargetGroupArgs =
            TargetGroupArgs(
                TargetType = "ip",
                Port = 3000,
                Protocol = "HTTP",
                VpcId = defaultVpc.Id,
                HealthCheck = TargetGroupHealthCheckArgs(Interval = 30, Path = "/health")
                )

        let webTargetGroup = TargetGroup(prefixMastodonResource "web-tg", webTargetGroupArgs)

        let streamingTargetGroupArgs =
            TargetGroupArgs(
                TargetType = "ip",
                Port = 4000,
                Protocol = "HTTP",
                VpcId = defaultVpc.Id,
                HealthCheck = TargetGroupHealthCheckArgs(Interval = 30, Path = "/api/v1/streaming/health")
            )
        
        let streamingTargetGroup = TargetGroup(prefixMastodonResource "streaming-tg", streamingTargetGroupArgs)

        let httpDefaultAction =
            ListenerDefaultActionArgs(
                Type = "redirect",
                Redirect =
                    ListenerDefaultActionRedirectArgs(
                        Port = "443",
                        Protocol = "HTTPS",
                        StatusCode = "HTTP_301"
                    )
            )
        
        let httpListenerArgs = ListenerArgs(
                LoadBalancerArn = loadBalancer.Arn,
                Port = 80, 
                Protocol = "HTTP",
                DefaultActions = inputList [ input httpDefaultAction ]
            )
        
        Listener(prefixMastodonResource "http-listener", httpListenerArgs) |> ignore

        let httpsDefaultAction =
            ListenerDefaultActionArgs(
                Type = "forward",                
                TargetGroupArn = webTargetGroup.Arn
            )

        let httpsListenerArgs = 
            ListenerArgs(
                LoadBalancerArn = loadBalancer.Arn,
                Port = 443,
                Protocol = "HTTPS",
                SslPolicy = "ELBSecurityPolicy-2016-08",
                CertificateArn = io (cert.Apply(fun cert -> cert.Arn)),
                DefaultActions =  inputList [ input httpsDefaultAction ]
            )
        
        let httpsListener = Listener(prefixMastodonResource "https-listener", httpsListenerArgs)

        let listRuleConditionPathPatternArgs = ListenerRuleConditionPathPatternArgs(
            Values = inputList  [ input "/api/v1/streaming"]
        )

        let listenerRuleConditionArgs = ListenerRuleConditionArgs(
            PathPattern = listRuleConditionPathPatternArgs
        )

        let listenerRuleActionArgs = ListenerRuleActionArgs(
            Type = "forward",
            TargetGroupArn = streamingTargetGroup.Arn
        )

        let listenerRuleArgs = ListenerRuleArgs(
            ListenerArn = httpsListener.Arn,
            Priority = 1,
            Conditions = inputList [input listenerRuleConditionArgs ],
            Actions = inputList [input listenerRuleActionArgs]
        )

        ListenerRule(prefixMastodonResource "streaming-api-path-rule",listenerRuleArgs) |> ignore

        (*
----------------------------------------
Container Task Definitions
----------------------------------------
*)
        let containerDefinitionsList =
            System.Collections.Generic.Dictionary<string, Awsx.Ecs.Inputs.TaskDefinitionContainerDefinitionArgs>()

        let postgresContainer = 
            match runMode with
                | Maintenance | Debug -> 
                    let taskDefinitionContainerDefinitionArgs =
                        Awsx.Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
                            Image = "postgres:latest",
                                Command =
                                    inputList [ input "bash"
                                                input "-c"
                                                input "while true; do sleep 3600; done" ],
                                Essential = false
                            )

                    containerDefinitionsList.Add("psql", taskDefinitionContainerDefinitionArgs)
                    ()
                | Production -> ()


        let webContainerportMappingArgs =
            Awsx.Ecs.Inputs.TaskDefinitionPortMappingArgs(ContainerPort = 3000, TargetGroup = webTargetGroup)

        let webContainerCommand = 
            match runMode with
            | Maintenance | Debug -> inputList [ input "bash"; input "-c"; input "while true; do sleep 3600; done" ]
            | Production ->  inputList [ input "bash"; input "-c"; input "rm -f /mastodon/tmp/pids/server.pid; bundle exec rails s -p 3000" ]
        
        let webContainer =
            Awsx.Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
                Image = "tootsuite/mastodon:v4.1.1",
                Command = webContainerCommand,
                Cpu = 256,
                Memory = 512,
                Essential = true,
                Environment = mastodonContainerEnvVariables,
                PortMappings = inputList [ input webContainerportMappingArgs ]
            )

        containerDefinitionsList.Add(prefixMastodonResource "web", webContainer)


        let streamingContainerportMappingArgs =
            Awsx.Ecs.Inputs.TaskDefinitionPortMappingArgs(ContainerPort = 4000, TargetGroup = streamingTargetGroup)

        let streamingContainer = Awsx.Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
            Image = "tootsuite/mastodon:v4.1.1",
            Command =
                inputList [ input "bash"
                            input "-c"
                            input "node ./streaming" ],
            Cpu = 256,
            Memory = 256,
            Essential = true,
            Environment = mastodonContainerEnvVariables,
            PortMappings = inputList[ input streamingContainerportMappingArgs ]
        )

        containerDefinitionsList.Add(prefixMastodonResource "streaming",streamingContainer)

        let sidekiqContainer = Awsx.Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
            Image = "tootsuite/mastodon:v4.1.1",
            Command =
                inputList [ input "bash"
                            input "-c"
                            input "bundle exec sidekiq" ],
            Cpu = 256,
            Memory = 256,
            Environment = mastodonContainerEnvVariables,
            Essential = true
        )

        containerDefinitionsList.Add(prefixMastodonResource "sidekiq",sidekiqContainer)

        (*
----------------------------------------
Fargate Service
----------------------------------------
*)

        let assumeRolePolicy =
            @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [
                {
                    ""Effect"": ""Allow"",
                    ""Principal"": {
                        ""Service"": ""ecs-tasks.amazonaws.com""
                    },
                    ""Action"": ""sts:AssumeRole""
                }
            ]
        }"

        let policiy =
            @"{
                    ""Version"": ""2012-10-17"",
                    ""Statement"": [
                        {
                            ""Effect"": ""Allow"",
                            ""Action"": [
                                ""ssmmessages:CreateControlChannel"",
                                ""ssmmessages:CreateDataChannel"",
                                ""ssmmessages:OpenControlChannel"",
                                ""ssmmessages:OpenDataChannel""
                            ],
                            ""Resource"": ""*""
                        }
                    ]
                }"


        let taskPolicy =
            Policy(prefixMastodonResource "task-policy", PolicyArgs(PolicyDocument = policiy))


        let taskRole =
            Role(
                prefixMastodonResource "task-role",
                RoleArgs(AssumeRolePolicy = assumeRolePolicy, ManagedPolicyArns = inputList [ io taskPolicy.Arn ])
            )

        let defaultTaskRoleWithPolicy =
            Awsx.Awsx.Inputs.DefaultRoleWithPolicyArgs(RoleArn = taskRole.Arn)


        let fargateServiceTaskDefinitionArgs =
            match runMode with 
                | Maintenance | Debug -> Awsx.Ecs.Inputs.FargateServiceTaskDefinitionArgs(
                    Containers = containerDefinitionsList,
                    TaskRole = defaultTaskRoleWithPolicy
                    )
                | Production -> Awsx.Ecs.Inputs.FargateServiceTaskDefinitionArgs(
                    Containers = containerDefinitionsList
                    )

        let networkConfiguration =
            ServiceNetworkConfigurationArgs(
                AssignPublicIp = true,
                Subnets = inputList (defaultSubnetIds |> List.map io),
                SecurityGroups = inputList [ io ecsSecurityGroup.Id ]
            )

        let enableExecutCommand = 
            match runMode with
                 | Maintenance | Debug -> true
                 | Production -> false

        let serviceArgs =
            Awsx.Ecs.FargateServiceArgs(
                Cluster = cluster.Arn,
                DesiredCount = 1,
                EnableExecuteCommand = enableExecutCommand,
                TaskDefinitionArgs = fargateServiceTaskDefinitionArgs,
                NetworkConfiguration = networkConfiguration
            )

        Awsx.Ecs.FargateService(prefixMastodonResource "fargate-service", serviceArgs) |> ignore

        ()
