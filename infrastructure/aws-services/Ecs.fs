namespace MastodonAwsServices

module Ecs =
    
    open Pulumi.FSharp 
    open Pulumi.Awsx

    let createEcs () =

        // - Cluster
        let cluster = Pulumi.Aws.Ecs.Cluster("mastodon-ecs-cluster")

        // - Application Loadbalancer
        // let loadBalancerArgs =
        //     LoadBalancerArgs(LoadBalancerType = input (string LoadBalancerType.Application))

        let loadBalancer =
            Lb.ApplicationLoadBalancer("mastodon-load-balancer")

        // - Service
        // - Task Definitions
        //   - Container Definitions
        //     - parameter store values and secrets for all Mastodon env variables
        let taskDefinitionPortMappingArgs = 
            Ecs.Inputs.TaskDefinitionPortMappingArgs(TargetGroup = loadBalancer.DefaultTargetGroup)
       
        let taskDefinitionContainerDefinitionArgs = 
            Ecs.Inputs.TaskDefinitionContainerDefinitionArgs(Image = "nginx:latest",
                Cpu = 512,
                Memory = 128,
                Essential = true,
                PortMappings = inputList[ input taskDefinitionPortMappingArgs ]
            )

        let fargateServiceTaskDefinitionArgs =
            Ecs.Inputs.FargateServiceTaskDefinitionArgs(
                Container = taskDefinitionContainerDefinitionArgs
            )

        let serviceArgs =
            Ecs.FargateServiceArgs(
                Cluster = cluster.Arn,
                AssignPublicIp = true,
                DesiredCount = 1,
                TaskDefinitionArgs = fargateServiceTaskDefinitionArgs
            )

        let service = Ecs.FargateService("testservice", serviceArgs)

        [ ("ecsCluster", cluster.Id :> obj) ]
        //   ("alb", loadBalancer.Urn :> obj)
        //   ("lbUrl",  "":> obj) ]
// - Roles(Access to RDS and Elasticache)


// return new Dictionary<string, object?>
// {
//     ["url"] = lb.LoadBalancer.Apply(loadBalancer => loadBalancer.DnsName),
// };
