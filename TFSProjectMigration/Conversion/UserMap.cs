using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace TFSProjectMigration.Conversion.Users
{
    public class UserMap
    {
        public List<string> SourceNames { get; set; } = new List<string>();

        public List<string> TargetNames { get; set; } = new List<string>();

        public Dictionary<string, string> MappedUsers { get; set; } = new Dictionary<string, string>();

        public void SaveToDisk(string filename)
        {
            using (var tw = File.CreateText(filename))
            {
                var serializer = JsonSerializer.Create();
                serializer.Serialize(tw, this);
            }
        }

        public void ReadFromDisk(string filename)
        {
            using (var tw = File.OpenText(filename))
            {
                var serializer = JsonSerializer.Create();
                var mu = serializer.Deserialize<UserMap>(new JsonTextReader(tw));

                MappedUsers = mu.MappedUsers;
                SourceNames = mu.SourceNames;
                TargetNames = mu.TargetNames;
            }
        }


        internal void Map(string displayName1, string displayName2)
        {
            MappedUsers[displayName1] = displayName2;
        }

        internal bool TryGetValue(string value, out string user)
        {
            return MappedUsers.TryGetValue(value, out user);
        }
    }
}