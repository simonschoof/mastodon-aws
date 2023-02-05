namespace MastodonAwsServices

module BaseInfrastructure =

    open Pulumi.Aws.Ec2
    open Pulumi.Aws.Ec2.Inputs
    open Pulumi.FSharp

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

        SecurityGroup("rds-security-group", rdsSecurityGroupArgs)


    let ecsSecurityGroup =
        let ecsSecurityGroupArgs =
            SecurityGroupArgs(Description = "Allow outbound traffic from ECS to RDS")

        SecurityGroup("ecs-security-group", ecsSecurityGroupArgs)


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

        SecurityGroupRule("mastodon-rds-inbound-tcp-security-group-rule", securityGroupRuleArgs)
    
    let ecsSecurityGroupIp4AllTrafficInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = ecsSecurityGroup.Id,
                Type = "ingress",
                FromPort = 0,
                ToPort = 65535,
                Protocol = "-1",
                CidrBlocks = inputList [ input "0.0.0.0/0" ])

        SecurityGroupRule("mastodon-ecs-inbound-all-ip4-security-group-rule", securityGroupRuleArgs)
    
    let ecsSecurityGroupIp6AllTrafficInboundRule =
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = ecsSecurityGroup.Id,
                Type = "ingress",
                FromPort = 0,
                ToPort = 65535,
                Protocol = "-1",
                Ipv6CidrBlocks = inputList [ input "::/0" ])
                
        SecurityGroupRule("mastodon-ecs-inbound-all-ip6-security-group-rule", securityGroupRuleArgs)

    let ecsSecurityGroupIp4AllTcpOutboundRule = 
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = ecsSecurityGroup.Id,
                Type = "egress",
                FromPort = 0,
                ToPort = 65535,
                Protocol = "tcp",
                CidrBlocks = inputList [ input "0.0.0.0/0" ])

        SecurityGroupRule("mastodon-ecs-outbound-all-tcp-ip4-security-group-rule", securityGroupRuleArgs)

    let ecsSecurityGroupIp6AllTcpOutboundRule = 
        let securityGroupRuleArgs =
            SecurityGroupRuleArgs(
                SecurityGroupId = ecsSecurityGroup.Id,
                Type = "egress",
                FromPort = 0,
                ToPort = 65535,
                Protocol = "tcp",
                Ipv6CidrBlocks = inputList [ input "::/0" ])

        SecurityGroupRule("mastodon-ecs-outbound-all-tcp-ip6-security-group-rule", securityGroupRuleArgs)


//[ ("rdsSecurityGroup", rdsSecurityGroup.Id :> obj)
//  ("ecsSecurityGroup", ecsSecurityGroup.Id :> obj) ]
