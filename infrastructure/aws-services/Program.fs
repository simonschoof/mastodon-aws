module Program

open MastodonAwsServices.ElastiCache
open MastodonAwsServices.Rds
open MastodonAwsServices.S3AndCloudFront
open MastodonAwsServices.Ecs
open Pulumi.FSharp

let infra () =

  createBucketAndDistribution () 
  createRdsCluster ()
  createElastiCacheCluster ()
  createEcs ()
  
  dict []

[<EntryPoint>]
let main _ =
  Deployment.run infra
