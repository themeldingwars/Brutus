using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Brutus.Shared
{
    public class Result : Dictionary<uint, Match>
    {
        public void Save(string file) => Save(this, file);
        public static void Save(Result result, string file)
        {
            string json = JsonConvert.SerializeObject(result, Formatting.Indented, new JsonSerializerSettings 
            { 
                
            });
            File.WriteAllText(file, json);
        }
        
        public static Result Load(string file)
        {
            string json = File.ReadAllText(file);
            return JsonConvert.DeserializeObject<Result>(json, new JsonSerializerSettings 
            { 
                
            });
        }
    }
}