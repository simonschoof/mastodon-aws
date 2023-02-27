namespace MastodonAwsServices

module Rds =

    open Pulumi
    open Pulumi.Aws.Rds
    open Pulumi.Aws.Rds.Inputs
    open Pulumi.FSharp
    open MastodonAwsServices
    open MastodonAwsServices.Ec2
    open MastodonAwsServices.Config.Values

    let createRdsCluster () =

        let clusterServerlessv2ScalingConfigurationArgs =
            ClusterServerlessv2ScalingConfigurationArgs(MaxCapacity = 1.0, MinCapacity = 0.5)

        let cluster =

            let clusterArgs =
                ClusterArgs(
                    ClusterIdentifier = prefixMastodonResource "rds-cluster-identifier",
                    Engine = "aurora-postgresql",
                    EngineMode = "provisioned",
                    EngineVersion = "14.5",
                    DatabaseName = "mastodon",
                    MasterUsername = "postgres",
                    MasterPassword = io (Output.CreateSecret rdsDbMasterPassword),
                    SkipFinalSnapshot = false,
                    FinalSnapshotIdentifier = "mastodon-rds-final-snapshot",
                    ApplyImmediately = true,
                    DeletionProtection = true,
                    Serverlessv2ScalingConfiguration = clusterServerlessv2ScalingConfigurationArgs,
                    VpcSecurityGroupIds = inputList [ io rdsSecurityGroup.Id ]
                )

            Cluster(prefixMastodonResource "rds-cluster", clusterArgs)

        let clusterInstanceArgs =
            ClusterInstanceArgs(
                ClusterIdentifier = cluster.Id,
                InstanceClass = "db.serverless",
                Engine = cluster.Engine,
                EngineVersion = cluster.EngineVersion
            )

        let clusterInstance =
            ClusterInstance(prefixMastodonResource "rds-cluster-instance", clusterInstanceArgs)

        [ ("rdsClusterName", cluster.Id :> obj)
          ("rdsClusterInstanceName", clusterInstance.Id :> obj) ]
