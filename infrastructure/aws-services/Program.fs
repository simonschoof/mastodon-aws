module Program

open MastodonAwsServices.ElastiCache
open MastodonAwsServices.Rds
open MastodonAwsServices.S3AndCloudFront
open MastodonAwsServices.Ecs
open Pulumi.FSharp

let infra () =

  let outputs = 
   createBucketAndDistribution() 
   @ createRdsCluster()
   // @ createElastiCacheCluster()
   //@  createEcs()
  
  dict outputs

[<EntryPoint>]
let main _ =
  Deployment.run infra
