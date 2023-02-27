namespace MastodonAwsServices 

module ElastiCache =

    open Pulumi.FSharp
    open Pulumi.Aws.ElastiCache
    open MastodonAwsServices.Ec2
    open MastodonAwsServices.Config.Values

    let createElastiCacheCluster () =

        let clusterArgs = ClusterArgs(
            Engine = "redis",
            EngineVersion = "7.0",
            NodeType = "cache.t3.micro",
            NumCacheNodes = 1,
            ParameterGroupName = "default.redis7",
            Port = 6379,
            ApplyImmediately = true,
            SecurityGroupIds = inputList [ io elasticacheSecurityGroup.Id ]
        )

        let cluster = Cluster(prefixMastodonResource "elasticache-cluster", clusterArgs)

        ()
