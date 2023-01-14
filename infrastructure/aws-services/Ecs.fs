namespace MastodonAwsServices

module Ecs =
    
    open Pulumi.FSharp 
    open Pulumi.Awsx

    let createEcs () =

        // - Cluster
        let cluster = Pulumi.Aws.Ecs.Cluster("mastodon-ecs-cluster")

        // Certificate
        let getCertificateInvokeArgs = Pulumi.Aws.Acm.GetCertificateInvokeArgs(
            Domain = "social.simonschoof.com",
            MostRecent = true,
            Types = inputList[ input "AMAZON_ISSUED" ]
        )
        let cert = Pulumi.Aws.Acm.GetCertificate.Invoke(getCertificateInvokeArgs)
        
        // - Application Loadbalancer
        let httpsListenerArgs = Lb.Inputs.ListenerArgs(
            Port = 443,
            Protocol = "HTTPS",
            SslPolicy = "ELBSecurityPolicy-2016-08",
            CertificateArn = io (cert.Apply(fun cert -> cert.Arn))
        )

        let defaultAction = Pulumi.Aws.LB.Inputs.ListenerDefaultActionArgs(
            Type = "redirect",
            Redirect = Pulumi.Aws.LB.Inputs.ListenerDefaultActionRedirectArgs(
                Port = "443",
                Protocol = "HTTPS",
                StatusCode = "HTTP_301"
            )
        )

        let httpListenerArgs = Lb.Inputs.ListenerArgs(
            Port = 80,
            Protocol = "HTTP",
            DefaultActions = inputList[ input defaultAction ]
        )

        let listenerList = System.Collections.Generic.List<Lb.Inputs.ListenerArgs>()
        listenerList.Add(httpListenerArgs)
        listenerList.Add(httpsListenerArgs) 
       
        let applicationLoadBalancerArgs = Lb.ApplicationLoadBalancerArgs(
            Listeners = listenerList
        )

        let loadBalancer =
            Lb.ApplicationLoadBalancer("mastodon-load-balancer", applicationLoadBalancerArgs)

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
