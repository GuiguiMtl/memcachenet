### MemCacheNet

## Settings

It is possible to configure the MemCache Server settings using appsetting.json

Important settings are:

- `MaxKeys` : Define the max number of keys the cache can hold before evicting keys
- `MaxKeySizeBytes`: Define the max size in bytes of a key
- `MaxDataSizeBytes`: Define the max size of the data in bytes
- `MaxTotalCacheSizeBytes`: Define the max total size of all the data in the cache
- `MaxConcurrentConnections`: Define the maximum total concurrent connections
- `ConnectionIdleTimeoutSeconds`: Define the timeout before an idle connection is closed
- `ReadTimeoutSeconds`: Define the timeout before timing out a connection that has starting sending data


## MemCache


The MemCache is thread safe using `ConcurrentDictionary` to manage concurrent access to the cache data itself.

The MemCache can also be provided an `IEvictionPolicyManager` that will manage the eviction of keys once the max number of keys have been reached.

## LRUEvictionPolicyManager

The default included implementation of the `IEvictionPolicyManager` is the `LRUEvictionPolicyManager` that will evict the Least Recently Used key using a simple `LinkedList` that is updated every time a key is being accessed.

Everytime a key is accessed it is put back at the top of the list.

When we try to remove a key we remove the last key of the list.

`LRUEvictionPolicyManager` is also thread safe managing concurrent access with a `SemaphoreSlim` that makes sure that only one concurrent access is done to list of keys.


## MemCache Server

The server supports the basic memcache command `GET`, `SET`, `DELETE` 

The TCP connections are handle using a `TCPListener` that waits for TCP connections.

The number of connection is limited by a given number of `SemaphoreSlim` that are released as connections are closed.

Incoming data from TCP connections are handle by `MemCacheConnectionHandler`. 

The connection handler uses the new .Net `System.IO.Pipelines` to read the incoming data stream from the Socket the most efficiently possible avoiding as much as possible converting bytes to string back to bytes to be stored.

With hindsight this was a pretty complicated task due to different format of command that be received (especially the SET command with a second block of data) and the management of invalid command with invalid format.


## MemCacheCommand

The `MemCacheCommandParser` will parse the bytes of the command to define what is the command that was received and returns a `MemCacheCommand`

The `MemCacheCommand` uses the CommandPattern and each command implements `HandleAsync(MemCacheCommandHandler handler)` while the `MemCacheHandlerCommandHandler`  implements a `HandleCommandAsync` for each command available.

This allows for easier extension in the future for new commands.


## Expiration Managing

The expiration of cache items is *lazily* managed. Keys are removed as they are accessed if they are expired.

I implemented an active expiration service `ExpirationManagerService` but it is not enabled as it can take valuable ressources and lock the cache just for key eviction.

## Integration tests

To run the integration tests, the MemCacheServer must be running.

## MemCacheLoadTester

Using AI, I implemented a simple version of a LoadTester that makes sure that the MemCacheNet works as expected under load. The MemCacheNet does not work as fast as an completely optimized memcache.

To use it run `./MemCacheLoadTester help` to have the list of options.

## Limitations

As with all .Net application that are sensitive to latency, garbage collection will have an impact on the performances.

## TODO

When the cache total max size is reached, we return an error. Instead we should be removing as many keys as possible using the EvictionPolicy until there is enough space to store the key.