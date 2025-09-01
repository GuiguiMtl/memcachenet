// using System.Text;

// namespace memcachenet.MemCacheServer;

// public class MemCacheCommandLineParser(int maxKeySize, int maxDataSize)
// {
//     public IMemCacheCommand ParseCommand(string commandLine)
//     {
//         string[] parts = commandLine.Split(' ');
//         string command = parts[0].ToLower();

//         switch (command)
//         {
//             case "get":
//                 return HandleGetCommand(parts);
//             case "set":
//                 return HandleSetCommand(parts);
//             case "delete":
//                 return HandleDeleteCommand(parts);
//             default:
//                 return HandleInvaliCommand(parts);
//         }
//     }

//     private IMemCacheCommand HandleInvaliCommand(string[] parts)
//     {
//         throw new NotImplementedException();
//     }

//     private IMemCacheCommand HandleDeleteCommand(string[] parts)
//     {
//         throw new NotImplementedException();
//     }

//     private IMemCacheCommand HandleSetCommand(string[] parts)
//     {
//         ushort flag;
//         uint expirationTime;
//         bool noReply = false;
//         // Validate the number of parameters
//         if (parts.Length < 5)
//         {
//             return new InvalidMemCacheCommand();
//         }
        
//         // Validate the size of the key
//         if (parts[1].Length > maxKeySize)
//         {
//             return new InvalidMemCacheCommand();
//         }
        
//         // Validate the flag is valid
//         if(!UInt16.TryParse(parts[2], out flag))
//         {
//             return new InvalidMemCacheCommand();
//         }

//         if (!UInt32.TryParse(parts[3], out expirationTime))
//         {
//             return new InvalidMemCacheCommand();
//         }
        
//         // Validate the size of the data
//         if(parts[4].Length > maxDataSize)
//         {
//             return new InvalidMemCacheCommand();
//         }
        
//         // Handle the non required noreply parameter
//         if (parts.Length == 6)
//         {
//             if (!parts[5].Equals("noreply", StringComparison.InvariantCultureIgnoreCase))
//             {
//                 return new InvalidMemCacheCommand();
//             }

//             noReply = true;
//         }

//         return new SetMemCacheCommand
//         {
//             Key = parts[1],
//             Flags = flag,
//             Expiration = expirationTime,
//             NoReply = noReply,
//             Data = Encoding.UTF8.GetBytes(parts[4]),
//         };
//     }

//     private IMemCacheCommand HandleGetCommand(string[] parts)
//     {
//         if (parts.Length != 2)
//         {
//             return new InvalidMemCacheCommand();
//         }
//         if (parts[1].Length > maxKeySize)
//         {
//             return new InvalidMemCacheCommand();
//         }
//         return new GetMemCacheCommand
//         {
//             Keys = []
//         };
//     }
// }
