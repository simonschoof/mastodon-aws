namespace MastodonAwsServices

module Ecs =

    open Pulumi
    open Pulumi.FSharp
    open Pulumi.Awsx
    open Pulumi.Aws.Iam
    open MastodonAwsServices.Ec2
    open MastodonAwsServices.Config.Values

    // TODO: Cleanup code and provide a debug flag which allows exec-command, starts a psql and mastodon web container. The web container is just running a bash.
    // Switch on final snapshots and deletion protections
    // TODO: Refine the outbound security group rules to only allow outbound connection to needed resources.
    // TODO: Allow SSL connections to RDS, S3 and Redis only(https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/CHAP_Security.html#CHAP_Security.InboundPorts)

    let createEcs () =

        (*
--------------------
ECS Cluster
--------------------
*)
        let cluster =
            Pulumi.Aws.Ecs.Cluster(prefixMastodonResource "ecs-cluster")

        (*
--------------------
Certificate
--------------------
*)
        // TODO: Find a way to get the certificate from the ACM without setting the domain name
        let getCertificateInvokeArgs =
            Pulumi.Aws.Acm.GetCertificateInvokeArgs(
                Domain = "social.simonschoof.com",
                MostRecent = true,
                Types = inputList [ input "AMAZON_ISSUED" ]
            )

        let cert =
            Pulumi.Aws.Acm.GetCertificate.Invoke(getCertificateInvokeArgs)

        (*
--------------------
Application Load Balancer
--------------------
*)
        let loadBalancerArgs = Pulumi.Aws.LB.LoadBalancerArgs(
            IpAddressType = "ipv4",
            LoadBalancerType = "application",
            SecurityGroups = inputList [ io loadBalancerSecurityGroup.Id],
            Subnets = inputList (defaultSubnetIds |> List.map io)
        )

        let loadBalancer = Pulumi.Aws.LB.LoadBalancer(prefixMastodonResource "load-balancer", loadBalancerArgs)

        // let listenerList =
        //     System.Collections.Generic.List<Lb.Inputs.ListenerArgs>()

        let webTargetGroupArgs =
            Pulumi.Aws.LB.TargetGroupArgs(
                TargetType = "ip",
                Port = 3000,
                Protocol = "HTTP",
                VpcId = defaultVpc.Id,
                HealthCheck = Pulumi.Aws.LB.Inputs.TargetGroupHealthCheckArgs(Interval = 30, Path = "/health")
                )

        let webTargetGroup = Pulumi.Aws.LB.TargetGroup(prefixMastodonResource "web-tg", webTargetGroupArgs)

        let streamingTargetGroupArgs =
            Pulumi.Aws.LB.TargetGroupArgs(
                TargetType = "ip",
                Port = 4000,
                Protocol = "HTTP",
                VpcId = defaultVpc.Id,
                HealthCheck = Pulumi.Aws.LB.Inputs.TargetGroupHealthCheckArgs(Interval = 30, Path = "/api/v1/streaming/health")
            )
        
        let streamingTargetGroup = Pulumi.Aws.LB.TargetGroup(prefixMastodonResource "streaming-tg", streamingTargetGroupArgs)

        let httpDefaultAction =
            Pulumi.Aws.LB.Inputs.ListenerDefaultActionArgs(
                Type = "redirect",
                Redirect =
                    Pulumi.Aws.LB.Inputs.ListenerDefaultActionRedirectArgs(
                        Port = "443",
                        Protocol = "HTTPS",
                        StatusCode = "HTTP_301"
                    )
            )

        // let httpListenerArgs =
        //     Lb.Inputs.ListenerArgs(Port = 80, Protocol = "HTTP", DefaultActions = inputList [ input defaultAction ])
        
        let httpListenerArgs = Pulumi.Aws.LB.ListenerArgs(
                LoadBalancerArn = loadBalancer.Arn,
                Port = 80, 
                Protocol = "HTTP",
                DefaultActions = inputList [ input httpDefaultAction ]
            )
        
        let httpListener = Pulumi.Aws.LB.Listener(prefixMastodonResource "http-listener", httpListenerArgs)

        // listenerList.Add(httpListenerArgs)

        let httpsDefaultAction =
            Pulumi.Aws.LB.Inputs.ListenerDefaultActionArgs(
                Type = "forward",                
                TargetGroupArn = webTargetGroup.Arn
            )

        let httpsListenerArgs = 
            Pulumi.Aws.LB.ListenerArgs(
                LoadBalancerArn = loadBalancer.Arn,
                Port = 443,
                Protocol = "HTTPS",
                SslPolicy = "ELBSecurityPolicy-2016-08",
                CertificateArn = io (cert.Apply(fun cert -> cert.Arn)),
                DefaultActions =  inputList [ input httpsDefaultAction ]
            )
        
        let httpsListener = Pulumi.Aws.LB.Listener(prefixMastodonResource "https-listener", httpsListenerArgs)

        let listRuleConditionPathPatternArgs = Pulumi.Aws.LB.Inputs.ListenerRuleConditionPathPatternArgs(
            Values = inputList  [ input "/api/v1/streaming"]
        )

        let listenerRuleConditionArgs = Pulumi.Aws.LB.Inputs.ListenerRuleConditionArgs(
            PathPattern = listRuleConditionPathPatternArgs
        )

        let listenerRuleActionForwardTargetGroupArgs = Pulumi.Aws.LB.Inputs.ListenerRuleActionForwardTargetGroupArgs(
            Arn = streamingTargetGroup.Arn
        )

        let listenerRuleActionForwardArgs = Pulumi.Aws.LB.Inputs.ListenerRuleActionForwardArgs(
            TargetGroups = inputList [input listenerRuleActionForwardTargetGroupArgs]
        )
        let listenerRuleActionArgs = Pulumi.Aws.LB.Inputs.ListenerRuleActionArgs(
            Type = "forward",
            TargetGroupArn = streamingTargetGroup.Arn
            //Forward = input listenerRuleActionForwardArgs
        )
        let listenerRuleArgs = Pulumi.Aws.LB.ListenerRuleArgs(
            ListenerArn = httpsListener.Arn,
            Priority = 1,
            Conditions = inputList [input listenerRuleConditionArgs ],
            Actions = inputList [input listenerRuleActionArgs]
        )

        let listenerRule = Pulumi.Aws.LB.ListenerRule(prefixMastodonResource "streaming-api-path-rule",listenerRuleArgs)

        // listenerList.Add(httpsListenerArgs)
        
        // let applicationLoadBalancerArgs =
        //     Lb.ApplicationLoadBalancerArgs(
        //         Listeners = listenerList,
        //         SecurityGroups = inputList [ io loadBalancerSecurityGroup.Id ]
        //     )

        // let loadBalancer =
        //     Lb.ApplicationLoadBalancer(prefixMastodonResource "load-balancer", applicationLoadBalancerArgs)

        (*
--------------------
Container Task Definitions
--------------------
*)
        let containerDefinitionsList =
            System.Collections.Generic.Dictionary<string, Ecs.Inputs.TaskDefinitionContainerDefinitionArgs>()

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
            Ecs.Inputs.TaskDefinitionPortMappingArgs(ContainerPort = 3000, TargetGroup = webTargetGroup)

        let webContainer =
            Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
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
            Ecs.Inputs.TaskDefinitionPortMappingArgs(ContainerPort = 4000, TargetGroup = streamingTargetGroup)

        let streamingContainer = Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
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

        let sidekiqContainer = Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
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
--------------------
Fargate Service
--------------------
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
            Awsx.Inputs.DefaultRoleWithPolicyArgs(RoleArn = taskRole.Arn)


        let fargateServiceTaskDefinitionArgs =
            Ecs.Inputs.FargateServiceTaskDefinitionArgs(
                Containers = containerDefinitionsList,
                TaskRole = defaultTaskRoleWithPolicy
            )

        let networkConfiguration =
            Pulumi.Aws.Ecs.Inputs.ServiceNetworkConfigurationArgs(
                AssignPublicIp = true,
                Subnets = inputList (defaultSubnetIds |> List.map io),
                SecurityGroups = inputList [ io ecsSecurityGroup.Id ]
            )

        let serviceArgs =
            Ecs.FargateServiceArgs(
                Cluster = cluster.Arn,
                DesiredCount = 1,
                EnableExecuteCommand = true,
                TaskDefinitionArgs = fargateServiceTaskDefinitionArgs,
                NetworkConfiguration = networkConfiguration
            )

        let service =
            Ecs.FargateService(prefixMastodonResource "fargate-service", serviceArgs)

        ()
