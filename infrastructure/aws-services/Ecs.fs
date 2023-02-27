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
        let cluster =
            Cluster(prefixMastodonResource "ecs-cluster")

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

        // let taskDefinitionPortMappingArgs =
        //     Ecs.Inputs.TaskDefinitionPortMappingArgs(TargetGroup = loadBalancer.DefaultTargetGroup)


        // let nginxTaskDefinitionContainerDefinitionArgs =
        //     Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
        //         Image = "nginx:latest",
        //         Cpu = 512,
        //         Memory = 128,
        //         Essential = true,
        //         PortMappings = inputList [ input taskDefinitionPortMappingArgs ]
        //     )

        // containerDefinitionsList.Add("nginx", nginxTaskDefinitionContainerDefinitionArgs)

        // let taskDefinitionContainerDefinitionArgs =
        //     Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
        //         Image = "postgres:latest",
        //         Command =
        //             inputList [ input "bash"
        //                         input "-c"
        //                         input "while true; do sleep 3600; done" ],
        //         Essential = false
        //     )

        // containerDefinitionsList.Add("psql", taskDefinitionContainerDefinitionArgs)


        let webContainerportMappingArgs =
            Awsx.Ecs.Inputs.TaskDefinitionPortMappingArgs(ContainerPort = 3000, TargetGroup = webTargetGroup)

        let webContainer =
            Awsx.Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
                Image = "tootsuite/mastodon:v4.1.0",
                Command =
                    inputList [ input "bash"
                                input "-c"
                                input "rm -f /mastodon/tmp/pids/server.pid; bundle exec rails s -p 3000" ],
                // Command =
                //     inputList [ input "bash"
                //                 input "-c"
                //                 input "while true; do sleep 3600; done" ],
                Cpu = 512,
                Memory = 512,
                Essential = true,
                Environment = mastodonContainerEnvVariables,
                PortMappings = inputList [ input webContainerportMappingArgs ]
            )

        containerDefinitionsList.Add(prefixMastodonResource "web", webContainer)


        let streamingContainerportMappingArgs =
            Awsx.Ecs.Inputs.TaskDefinitionPortMappingArgs(ContainerPort = 4000, TargetGroup = streamingTargetGroup)

        let streamingContainer = Awsx.Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
            Image = "tootsuite/mastodon:v4.1.0",
            Command =
                inputList [ input "bash"
                            input "-c"
                            input "node ./streaming" ],
            Cpu = 512,
            Memory = 512,
            Essential = true,
            Environment = mastodonContainerEnvVariables,
            PortMappings = inputList[ input streamingContainerportMappingArgs ]
        )

        containerDefinitionsList.Add(prefixMastodonResource "streaming",streamingContainer)

        let sidekiqContainer = Awsx.Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
            Image = "tootsuite/mastodon:v4.1.0",
            Command =
                inputList [ input "bash"
                            input "-c"
                            input "bundle exec sidekiq" ],
            Cpu = 512,
            Memory = 512,
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
            Awsx.Ecs.Inputs.FargateServiceTaskDefinitionArgs(
                Containers = containerDefinitionsList,
                TaskRole = defaultTaskRoleWithPolicy
            )

        let networkConfiguration =
            ServiceNetworkConfigurationArgs(
                AssignPublicIp = true,
                Subnets = inputList (defaultSubnetIds |> List.map io),
                SecurityGroups = inputList [ io ecsSecurityGroup.Id ]
            )

        let serviceArgs =
            Awsx.Ecs.FargateServiceArgs(
                Cluster = cluster.Arn,
                DesiredCount = 1,
                EnableExecuteCommand = true,
                TaskDefinitionArgs = fargateServiceTaskDefinitionArgs,
                NetworkConfiguration = networkConfiguration
            )

        Awsx.Ecs.FargateService(prefixMastodonResource "fargate-service", serviceArgs) |> ignore

        ()
