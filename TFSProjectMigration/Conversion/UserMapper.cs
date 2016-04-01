using Microsoft.TeamFoundation.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TFSProjectMigration.Conversion.Users
{
    public class UserMapper
    {
        private TfsProject SourceProject;
        private TfsProject TargetProject;

        public UserMap UsersMap { get; set; } = new UserMap();


        public UserMapper(TfsProject SourceProject, TfsProject TargetProject)
        {
            this.SourceProject = SourceProject;
            this.TargetProject = TargetProject;
        }

        public UserMap MapUserIds()
        {
            var sourceUsers = GetUsers(SourceProject);
            var targetUsers = GetUsers(TargetProject);

            UsersMap.SourceNames = sourceUsers.Select(a => a.DisplayName).ToList();
            UsersMap.TargetNames = targetUsers.Select(a => a.DisplayName).ToList();

            var targetUsersByMailAddress = targetUsers.Where(a => a != null && a.AccountName != null).GroupBy(a => a.MailAddress).ToDictionary(a => a.Key, a => a.ToList());
            var targetUsersByDisplayName = targetUsers.Where(a => a != null && a.AccountName != null).GroupBy(a => a.DisplayName).ToDictionary(a => a.Key, a => a.ToList());
            foreach (Identity user in sourceUsers)
            {
                List<Identity> identities;
                if (targetUsersByDisplayName.TryGetValue(user.DisplayName, out identities))
                {
                    UsersMap.Map(user.DisplayName, identities[0].DisplayName);
                }
                else if (targetUsersByMailAddress.TryGetValue(user.MailAddress, out identities))
                {
                    UsersMap.Map(user.DisplayName, identities[0].DisplayName);
                }
                else if (targetUsersByMailAddress.TryGetValue(user.DistinguishedName, out identities))
                {
                    UsersMap.Map(user.DisplayName, identities[0].DisplayName);
                }
            }

            return UsersMap;
        }

        
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
}