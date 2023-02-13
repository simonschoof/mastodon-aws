namespace MastodonAwsServices


module S3 = 
    open Pulumi.Aws.S3  
    
    let createBucket ()  = // Create an AWS resource (S3 Bucket)
        let bucket =
            let bucketName = "mastodon-s3-storage"
        
            let bucketArgs =
                BucketArgs(Acl = "private")
        
            Bucket(bucketName, bucketArgs)
    
        
        let bucketPublicAccessBlockArgs = BucketPublicAccessBlockArgs(
            Bucket = bucket.Id,
            BlockPublicAcls = true,
            BlockPublicPolicy = true,
            IgnorePublicAcls = true,
            RestrictPublicBuckets = true
        )
    
        BucketPublicAccessBlock("mastodon-s3-storage-public-access-block", bucketPublicAccessBlockArgs)
          
        // Export the name of the bucket
        [("bucketName", bucket.Id :> obj)]