using Microsoft.TeamFoundation.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UpdateSprintAndReleaseDates
{
    class Program
    {
        static void Main(string[] args)
        {
            TfsTeamProjectCollection tfs = new TfsTeamProjectCollection(new Uri("http://dev-app-001:8080/tfs/DefaultCollection"));
            tfs.Credentials = new NetworkCredential("bvs", "Bringme1", "CASSANDRA");
            tfs.Authenticate();

            Iterations i = new Iterations(tfs, "MyJames 2010");
            var iterations = i.PopulateIterations();
            var releases = i.GetReleases();
            var sprints = i.GetSprints();

            foreach (var item in releases)
            {
                var end = item.Fields.Contains("Actual Work Ends (Scrum v3)") ? item.Fields["Actual Work Ends (Scrum v3)"].Value : null;
                var start = item.Fields.Contains("Actual Work Starts (Scrum v3)") ? item.Fields["Actual Work Starts (Scrum v3)"].Value : null;
                if (start == null && end == null)
                    continue;

                string o;
                var iterationPath = "\\MyJames 2010\\Iteration" + item.IterationPath.Substring(12);
                if (iterations.TryGetValue(iterationPath, out o))
                {
                    i.UpdateIteration(o, (DateTime)start, (DateTime)end);
                }
            }

            foreach (var item in sprints)
            {
                var end = item.Fields.Contains("Sprint End (Scrum v3)") ? item.Fields["Sprint End (Scrum v3)"].Value : null;
                var start = item.Fields.Contains("Sprint Start (Scrum v3)") ? item.Fields["Sprint Start (Scrum v3)"].Value : null;
                if (start == null && end == null)
                    continue;

                string o;
                var iterationPath = "\\MyJames 2010\\Iteration" + item.IterationPath.Substring(12);
                if (iterations.TryGetValue(iterationPath, out o))
                {
                    i.UpdateIteration(o, (DateTime)start, (DateTime)end);
                }
            }

            
        }
    }
}
