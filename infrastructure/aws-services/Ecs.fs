namespace MastodonAwsServices

module Ecs = 
    open Pulumi.Aws.Ecs
    open Pulumi.Aws.Alb
    open Pulumi.FSharp
    
    let createEcs () =
    
        // - Cluster
        let cluster = Cluster("mastodon-ecs-cluster")
        // - Application Loadbalancer
        let loadBalancerArgs =
            LoadBalancerArgs(LoadBalancerType = input (string LoadBalancerType.Application))
    
        let loadBalancer = LoadBalancer("mastodon-load-balancer")
        // - Service
        // - Task Definitions
        //   - Container Definitions
        //     - parameter store values and secrets for all Mastodon env variables
        // - Roles(Access to RDS and Elasticache)
    
    
        [ ("ecsCluster", cluster.Id :> obj); ("alb", loadBalancer.Id :> obj) ]
