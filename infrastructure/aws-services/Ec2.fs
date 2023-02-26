namespace MastodonAwsServices

module Ec2 =

    open Pulumi.Aws.Ec2
    open Pulumi.Aws.Ec2.Inputs
    open Pulumi.FSharp
    open MastodonAwsServices.Config.Values

    let defaultVpc = DefaultVpc("default-vpc")

    let defaultSubnets =
        let subnetInvokeArgs =
            GetSubnetsInvokeArgs(
                Filters =
                    inputList [ input (
                                    GetSubnetsFilterInputArgs(Name = "vpc-id", Values = inputList [ io defaultVpc.Id ])
                                ) ]
            )

        GetSubnets.Invoke(subnetInvokeArgs)

    let defaultSubnetIds =
        [ defaultSubnets.Apply(fun subnets -> subnets.Ids[0])
          defaultSubnets.Apply(fun subnets -> subnets.Ids[1])
          defaultSubnets.Apply(fun subnets -> subnets.Ids[2]) ]

    let rdsSecurityGroup =
        let rdsSecurityGroupArgs =
            SecurityGroupArgs(Description = "Allow inbound traffic from ECS to RDS")

        SecurityGroup(prefixMastodonResource "rds-security-group", rdsSecurityGroupArgs)
    
    let elasticacheSecurityGroup =
        let elasticacheSecurityGroupArgs =
            SecurityGroupArgs(Description = "Allow inbound traffic from ECS to Elasticache")

        SecurityGroup(prefixMastodonResource "elasticache-security-group", elasticacheSecurityGroupArgs)


    let ecsSecurityGroup =
        let ecsSecurityGroupArgs =
            SecurityGroupArgs(Description = "Ecs Security Group")

        SecurityGroup(prefixMastodonResource "ecs-security-group", ecsSecurityGroupArgs)

    let loadBalancerSecurityGroup = 
        let loadBalancerSecurityGroupArgs = 
            SecurityGroupArgs(Description = "Loadbalancer Security Group")

        SecurityGroup(prefixMastodonResource "loadbalancer-security-group", loadBalancerSecurityGroupArgs)

    let rdsSecurityGroupInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = rdsSecurityGroup.Id,
                Type = "ingress",
                FromPort = 5432,
                ToPort = 5432,
                Protocol = "tcp",
                SourceSecurityGroupId = ecsSecurityGroup.Id
            )

        SecurityGroupRule(prefixMastodonResource "rds-inbound-tcp-security-group-rule", securityGroupRuleArgs)
    
    let elastiCacheSecurityGroupInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = elasticacheSecurityGroup.Id,
                Type = "ingress",
                FromPort = 6379,
                ToPort = 6379,
                Protocol = "tcp",
                SourceSecurityGroupId = ecsSecurityGroup.Id
            )

        SecurityGroupRule(prefixMastodonResource "elasticache-inbound-tcp-security-group-rule", securityGroupRuleArgs)
    
    let ecsSecurityGroupIp4HttpTrafficInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = ecsSecurityGroup.Id,
                Type = "ingress",
                FromPort = 80,
                ToPort = 80,
                Protocol = "tcp",
                SourceSecurityGroupId = loadBalancerSecurityGroup.Id)

        SecurityGroupRule(prefixMastodonResource "ecs-inbound-http-ip4-security-group-rule", securityGroupRuleArgs)

    let ecsSecurityGroupIp4MastodonWebTrafficInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = ecsSecurityGroup.Id,
                Type = "ingress",
                FromPort = 3000,
                ToPort = 3000,
                Protocol = "tcp",
                SourceSecurityGroupId = loadBalancerSecurityGroup.Id)

        SecurityGroupRule(prefixMastodonResource "ecs-inbound-mastodon-web-ip4-security-group-rule", securityGroupRuleArgs)

    let ecsSecurityGroupIp4MastodonStreamingTrafficInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = ecsSecurityGroup.Id,
                Type = "ingress",
                FromPort = 4000,
                ToPort = 4000,
                Protocol = "tcp",
                SourceSecurityGroupId = loadBalancerSecurityGroup.Id)

        SecurityGroupRule(prefixMastodonResource "ecs-inbound-mastodon-streaming-ip4-security-group-rule", securityGroupRuleArgs)

    let ecsSecurityGroupIp4AllTcpOutboundRule = 
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = ecsSecurityGroup.Id,
                Type = "egress",
                FromPort = 0,
                ToPort = 65535,
                Protocol = "tcp",
                CidrBlocks = inputList [ input "0.0.0.0/0" ])

        SecurityGroupRule(prefixMastodonResource"ecs-outbound-all-tcp-ip4-security-group-rule", securityGroupRuleArgs)

    let loadBalancerSecurityGroupIp4HttpTrafficInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = loadBalancerSecurityGroup.Id,
                Type = "ingress",
                FromPort = 80,
                ToPort = 80,
                Protocol = "tcp",
                CidrBlocks = inputList [ input "0.0.0.0/0"] )

        SecurityGroupRule(prefixMastodonResource "loadbalancer-inbound-http-security-group-rule", securityGroupRuleArgs)

    let loadBalancerSecurityGroupIp4HttpsTrafficInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = loadBalancerSecurityGroup.Id,
                Type = "ingress",
                FromPort = 443,
                ToPort = 443,
                Protocol = "tcp",
                CidrBlocks = inputList [ input "0.0.0.0/0"] )

        SecurityGroupRule(prefixMastodonResource "loadbalancer-inbound-https-security-group-rule", securityGroupRuleArgs)
    
    let loadBalancerSecurityGroupIp4AllTcpOutboundRule = 
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = loadBalancerSecurityGroup.Id,
                Type = "egress",
                FromPort = 0,
                ToPort = 65535,
                Protocol = "tcp",
                CidrBlocks = inputList [ input "0.0.0.0/0" ])

        SecurityGroupRule(prefixMastodonResource "loadbalancer-outbound-all-tcp-ip4-security-group-rule", securityGroupRuleArgs)


//[ ("rdsSecurityGroup", rdsSecurityGroup.Id :> obj)
//  ("ecsSecurityGroup", ecsSecurityGroup.Id :> obj) ]
