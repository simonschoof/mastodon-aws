namespace MastodonAwsServices

module Rds =

    open Pulumi
    open Pulumi.Aws.Rds
    open Pulumi.Aws.Rds.Inputs
    open Pulumi.FSharp
    open MastodonAwsServices.Config.Secrets
    open MastodonAwsServices.Ec2

    let createRdsCluster () =

        let rdsDbMasterPassword =
            getSecret ("mastodon/rds/db-master-password")

        let config = Config()

        let clusterServerlessv2ScalingConfigurationArgs =
            ClusterServerlessv2ScalingConfigurationArgs(MaxCapacity = 1.0, MinCapacity = 0.5)

        let cluster =

            let clusterArgs =
                ClusterArgs(
                    ClusterIdentifier = "mastodon-rds-cluster",
                    Engine = "aurora-postgresql",
                    EngineMode = "provisioned",
                    EngineVersion = "14.5",
                    DatabaseName = "mastodon",
                    MasterUsername = "postgres",
                    MasterPassword = io (Output.CreateSecret rdsDbMasterPassword),
                    SkipFinalSnapshot = true,
                    //FinalSnapshotIdentifier = "mastodon-rds-final-snapshot",
                    ApplyImmediately = true,
                    DeletionProtection = false,
                    Serverlessv2ScalingConfiguration = clusterServerlessv2ScalingConfigurationArgs,
                    VpcSecurityGroupIds = inputList [ io rdsSecurityGroup.Id ]
                )

            Cluster("mastodon-rds-cluster", clusterArgs)

        let clusterInstanceArgs =
            ClusterInstanceArgs(
                ClusterIdentifier = cluster.Id,
                InstanceClass = "db.serverless",
                Engine = cluster.Engine,
                EngineVersion = cluster.EngineVersion
            )

        let clusterInstance =
            ClusterInstance("mastodon-rds-cluster-instance", clusterInstanceArgs)

        [ ("rdsClusterName", cluster.Id :> obj)
          ("rdsClusterInstanceName", clusterInstance.Id :> obj) ]
