using Microsoft.TeamFoundation.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSProjectMigration.Conversion.Users
{
    public class UserMigration
    {
        public TfsProject SourceProject { get; set; }
        public TfsProject TargetProject { get; set; }

        public UserMigration(TfsProject SourceProject, TfsProject TargetProject)
        {
            this.SourceProject = SourceProject;
            this.TargetProject = TargetProject;
        }

        public void MapUserIds()
        {
            var sourceUserIds = GetUsers(SourceProject);
            var allUsers = GetUsers(TargetProject);
            var targetUserIds = allUsers.Where(a=> a != null && a.AccountName != null).GroupBy(a=> a.AccountName).ToDictionary(a=> a.Key, a => a.ToList());
           
            foreach (Identity user in sourceUserIds)
            {
                List<Identity> identities;
                if (targetUserIds.TryGetValue(user.AccountName, out identities))
                {
                    UsersMap[user.DisplayName] = identities[0].DisplayName;
                }
                else if (targetUserIds.TryGetValue(user.MailAddress, out identities))
                {
                    UsersMap[user.DisplayName] = identities[0].DisplayName;
                }
            }
        }

        public UserMap UsersMap { get; set; } = new UserMap();

        private static IEnumerable<Identity> GetUsers(TfsProject SourceProject)
        {
            HashSet<Identity> users = new HashSet<Identity>();
            Queue<Identity> queuedItems = new Queue<Identity>();

            IGroupSecurityService gss = SourceProject.collection.GetService<IGroupSecurityService>();
            Identity SIDS = gss.ReadIdentity(SearchFactor.AccountName, "Project Collection Valid Users", QueryMembership.Expanded);

            Identity[] sourceUserIds = gss.ReadIdentities(SearchFactor.Sid, SIDS.Members, QueryMembership.Direct);
            foreach (var item in sourceUserIds)
            {
                if (item != null)
                {
                    queuedItems.Enqueue(item);
                }
            }

            while (queuedItems.Any())
            {
                var firstSid = queuedItems.Dequeue();
                if (firstSid == null)
                    continue;

                try
                {
                    users.Add(firstSid);
                    if (firstSid.Members.Length == 0)
                        continue;

                    Identity[] members = gss.ReadIdentities(SearchFactor.Sid, firstSid.Members, QueryMembership.Direct);
                    foreach (var item in members)
                    {
                        if (item == null)
                            continue;

                        if (users.Add(item))
                        {
                            if (item.Type == IdentityType.WindowsUser)
                                continue;

                            queuedItems.Enqueue(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            return users;
        }
    }

    public class UserMap: Dictionary<string, string>
    {

    }
}
