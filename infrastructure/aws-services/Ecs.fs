namespace MastodonAwsServices

module Ecs =

    open Pulumi.FSharp
    open Pulumi.Awsx
    open Pulumi.Aws.Iam
    open MastodonAwsServices.Ec2

    // TODO: Check if I need a public ip on the Fargate service or if the load balancer is sufficient
    // TODO: Define security groups for rds, redis, ses and ecs accordingly to allow access from ecs to rds and redis.
    // TODO: Define a IAM policy for the ecs task role to allow access s3
    // TODO: Provide all ENV variables for each mastodon deployable as parameter store values and secrets
    // TODO: Prepare Database manually. Create user, database and schema.
    // TODO: Create a task definitions for each mastodon deployable web, streaming, sidekiq
    // TODO: Create a rule to route requests to the streaming api to the streaming container
    // TODO: Enable SES and create smtp credentials for mastodon
    // TODO: Run script before mastodon web container starts to setup or migrate database. I will do downtime deploys
    // TODO: Allow SSL connections to RDS, S3 and Redis only(https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/CHAP_Security.html#CHAP_Security.InboundPorts)
    let createEcs () =

        (*
--------------------
ECS Cluster
--------------------
*)         
        let cluster =
            Pulumi.Aws.Ecs.Cluster("mastodon-ecs-cluster")

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
        let listenerList =
            System.Collections.Generic.List<Lb.Inputs.ListenerArgs>()

        let defaultAction =
            Pulumi.Aws.LB.Inputs.ListenerDefaultActionArgs(
                Type = "redirect",
                Redirect =
                    Pulumi.Aws.LB.Inputs.ListenerDefaultActionRedirectArgs(
                        Port = "443",
                        Protocol = "HTTPS",
                        StatusCode = "HTTP_301"
                    )
            )

        let httpListenerArgs =
            Lb.Inputs.ListenerArgs(Port = 80, Protocol = "HTTP", DefaultActions = inputList [ input defaultAction ])
        
        listenerList.Add(httpListenerArgs)

        let httpsListenerArgs =
            Lb.Inputs.ListenerArgs(
                Port = 443,
                Protocol = "HTTPS",
                SslPolicy = "ELBSecurityPolicy-2016-08",
                CertificateArn = io (cert.Apply(fun cert -> cert.Arn))
            )

        listenerList.Add(httpsListenerArgs)

        let applicationLoadBalancerArgs =
            Lb.ApplicationLoadBalancerArgs(Listeners = listenerList)

        let loadBalancer =
            Lb.ApplicationLoadBalancer("mastodon-load-balancer", applicationLoadBalancerArgs)

        (*
--------------------
Container Task Definitions
--------------------
*)
        let containerDefinitionsList =
            System.Collections.Generic.Dictionary<string, Ecs.Inputs.TaskDefinitionContainerDefinitionArgs>()

        let taskDefinitionPortMappingArgs =
            Ecs.Inputs.TaskDefinitionPortMappingArgs(TargetGroup = loadBalancer.DefaultTargetGroup)
        

        let nginxTaskDefinitionContainerDefinitionArgs =
            Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
                Image = "nginx:latest",
                Cpu = 512,
                Memory = 128,
                Essential = true,
                PortMappings = inputList [ input taskDefinitionPortMappingArgs ]
            )

        containerDefinitionsList.Add("nginx", nginxTaskDefinitionContainerDefinitionArgs)

        let taskDefinitionContainerDefinitionArgs =
            Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
                Image = "postgres:latest",
                Command =
                    inputList [ input "bash"
                                input "-c"
                                input "while true; do sleep 3600; done" ],
                Essential = false
            )

        containerDefinitionsList.Add("psql", taskDefinitionContainerDefinitionArgs)

        // let webTargetGroupArgs = Pulumi.Aws.LB.TargetGroupArgs(
        //     Port = 3000,
        //     Protocol = "HTTP",
        //     VpcId = cluster.Arn,
        //     HealthCheck = Pulumi.Aws.LB.Inputs.TargetGroupHealthCheckArgs(
        //         Interval = 30,
        //         Path = "/health"
        //     )
        // )

        // let webTargetGroup = Pulumi.Aws.LB.TargetGroup("mastodon-web-tg", webTargetGroupArgs)

        // let webContainerportMappingArgs = Ecs.Inputs.TaskDefinitionPortMappingArgs(
        //     ContainerPort = 3000,
        //     TargetGroup = webTargetGroup)

        // let webContainer = Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
        //     Image = "tootsuite/mastodon:4.0.2",
        //     Command = inputList [input "bin/rails"; input "server"; input "-b"; input  "0.0.0.0"],
        //     Cpu = 512,
        //     Memory = 512,
        //     Essential = true,
        //     PortMappings = inputList[ input webContainerportMappingArgs ]
        // )

        // containerDefinitionsList.Add("web",webContainer)

        // let streamingContainerportMappingArgs = Ecs.Inputs.TaskDefinitionPortMappingArgs(ContainerPort = 4000)

        // let streamingContainer = Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
        //     Image = "tootsuite/mastodon:4.0.2",
        //     Command = inputList [ input "bin/streaming"],
        //     Cpu = 512,
        //     Memory = 512,
        //     Essential = true,
        //     PortMappings = inputList[ input streamingContainerportMappingArgs ]
        // )

        // containerDefinitionsList.Add("streaming",streamingContainer)


        // let sidekiqContainer = Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(
        //     Image = "tootsuite/mastodon:4.0.2",
        //     Command = inputList [ input "bin/streaming"],
        //     Cpu = 512,
        //     Memory = 512,
        //     Essential = true
        // )

        // containerDefinitionsList.Add("sidekiq",sidekiqContainer)

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
            Policy("taskPolicy", PolicyArgs(PolicyDocument = policiy))


        let taskRole =
            Role(
                "taskRole",
                RoleArgs(AssumeRolePolicy = assumeRolePolicy, ManagedPolicyArns = inputList [ io taskPolicy.Arn ])
            )

        let defaultTaskRoleWithPolicy =
            Awsx.Inputs.DefaultRoleWithPolicyArgs(RoleArn = taskRole.Arn)


        let fargateServiceTaskDefinitionArgs =
            Ecs.Inputs.FargateServiceTaskDefinitionArgs(
                Containers = containerDefinitionsList,
                TaskRole = defaultTaskRoleWithPolicy
            )

        let networkConfiguration =  Pulumi.Aws.Ecs.Inputs.ServiceNetworkConfigurationArgs(
                    AssignPublicIp = true,
                    Subnets = inputList (defaultSubnetIds |> List.map io),
                    SecurityGroups = inputList [ io ecsSecurityGroup.Id ]
                )

        let serviceArgs =
            Ecs.FargateServiceArgs(
                Cluster = cluster.Arn,
                //AssignPublicIp = true,
                DesiredCount = 1,
                EnableExecuteCommand = true,
                TaskDefinitionArgs = fargateServiceTaskDefinitionArgs,
                NetworkConfiguration = networkConfiguration
            )

        let service =
            Ecs.FargateService("testservice", serviceArgs)

        [ ("ecsCluster", cluster.Id :> obj) ]
//   ("alb", loadBalancer.Urn :> obj)
//   ("lbUrl",  "":> obj) ]

// return new Dictionary<string, object?>
// {
//     ["url"] = lb.LoadBalancer.Apply(loadBalancer => loadBalancer.DnsName),
// };
