using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.Server;
using log4net.Config;
using log4net;
using System.Windows.Controls;
using TFSProjectMigration.Conversion.WorkItems;
using TFSProjectMigration.Conversion.Users;

namespace TFSProjectMigration
{
    public class TestPlanMigration
    {
        ITestManagementTeamProject2 sourceproj;
        ITestManagementTeamProject2 destinationproj;
        public WorkItemIdMap WorkItemIdMap { get; set; }
        String projectName;
        private static readonly ILog logger = LogManager.GetLogger(typeof(TFSWorkItemMigrationUI));
        internal UserMap UserMapping { get; set; }

        public UserMap UsersMap { get; internal set; }

        public TestPlanMigration(TfsProject sourceTfs, TfsProject targetTfs)
        {
            this.sourceproj = GetProject(sourceTfs.collection, sourceTfs.project.Name);
            this.destinationproj = GetProject(targetTfs.collection, targetTfs.project.Name);
            
            projectName = sourceTfs.project.Name;
        }

        private ITestManagementTeamProject2 GetProject(TfsTeamProjectCollection tfs, string project)
        {            
            ITestManagementService2 tms = tfs.GetService<ITestManagementService2>();
            return (ITestManagementTeamProject2)tms.GetTeamProject(project);
        }

        public void CopyTestPlans()
        {
            int i = 1;
            int planCount= sourceproj.TestPlans.Query("Select * From TestPlan").Count;
            //delete Test Plans if any existing test plans.
            //foreach (ITestPlan destinationplan in destinationproj.TestPlans.Query("Select * From TestPlan"))
            //{

            //    System.Diagnostics.Debug.WriteLine("Deleting Plan - {0} : {1}", destinationplan.Id, destinationplan.Name);

            //    destinationplan.Delete(DeleteAction.ForceDeletion); ;

            //}
           
            foreach (ITestPlan2 sourceplan in sourceproj.TestPlans.Query("Select * From TestPlan"))
            {
                System.Diagnostics.Debug.WriteLine("Plan - {0} : {1}", sourceplan.Id, sourceplan.Name);

                ITestPlan2 destinationplan = null;
                if (WorkItemIdMap.Contains(sourceplan.Id))
                {
                    var newId = WorkItemIdMap[sourceplan.Id];
                    destinationplan =(ITestPlan2) destinationproj.TestPlans.Query("SELECT * from TestPlan").Where(a=> a.Id == newId).FirstOrDefault(); ;
                    
                }

                if (destinationplan == null)
                {
                    destinationplan = (ITestPlan2)destinationproj.TestPlans.Create();
                    destinationplan.Name = sourceplan.Name;

                    destinationplan.Save();
                    WorkItemIdMap.Map(sourceplan.Id, destinationplan.Id);
                }
                
                
                destinationplan.Description = sourceplan.Description;
                destinationplan.StartDate = sourceplan.StartDate;
                destinationplan.EndDate = sourceplan.EndDate;
                destinationplan.Status = sourceplan.Status;            
                


                //drill down to root test suites.
                if (sourceplan.RootSuite != null && sourceplan.RootSuite.Entries.Count > 0)
                {
                    CopyTestSuites(sourceplan, destinationplan);
                }

                destinationplan.Save();

                //progressBar.Dispatcher.BeginInvoke(new Action(delegate()
                //{
                //    float progress = (float)i / (float) planCount;

                //    progressBar.Value = ((float)i / (float) planCount) * 100;
                //}));
                i++;
            }

        }

        //Copy all Test suites from source plan to destination plan.
        private void CopyTestSuites(ITestPlan sourceplan, ITestPlan destinationplan)
        {
            ITestSuiteEntryCollection suites = sourceplan.RootSuite.Entries;
            CopyTestCases(sourceplan.RootSuite, destinationplan.RootSuite);

            foreach (ITestSuiteEntry suite_entry in suites)
            {
                IStaticTestSuite suite = suite_entry.TestSuite as IStaticTestSuite;
                if (suite != null)
                {
                    IStaticTestSuite newSuite = null;
                    if (WorkItemIdMap.Contains(suite.Id))
                    {
                        var newId = WorkItemIdMap[suite.Id];
                        newSuite = (IStaticTestSuite)destinationproj.TestSuites.Find(newId);

                    }
                    
                    if (newSuite == null)
                    {
                        newSuite = destinationproj.TestSuites.CreateStatic();
                        newSuite.Title = suite.Title;
                        
                        destinationplan.RootSuite.Entries.Add(newSuite);
                        destinationplan.Save();

                        WorkItemIdMap.Map(suite.Id, newSuite.Id);
                    }                                                           
                    
                    CopyTestCases(suite, newSuite);
                    if (suite.Entries.Count > 0)
                        CopySubTestSuites(suite, newSuite);
                }
            }

        }
        //Drill down and Copy all subTest suites from source root test suite to destination plan's root test suites.
        private void CopySubTestSuites(IStaticTestSuite parentsourceSuite, IStaticTestSuite parentdestinationSuite)
        {
            ITestSuiteEntryCollection suitcollection = parentsourceSuite.Entries;
            foreach (ITestSuiteEntry suite_entry in suitcollection)
            {
                IStaticTestSuite suite = suite_entry.TestSuite as IStaticTestSuite;
                if (suite != null)
                {

                    IStaticTestSuite subSuite;
                    subSuite = destinationproj.TestSuites.CreateStatic();


                    subSuite.Title = suite.Title;
                    parentdestinationSuite.Entries.Add(subSuite);
                    
                    CopyTestCases(suite, subSuite);

                    if (suite.Entries.Count > 0)
                        CopySubTestSuites(suite, subSuite);

                }
            }


        }

        //Copy all subTest suites from source root test suite to destination plan's root test suites.
        private void CopyTestCases(IStaticTestSuite sourcesuite, IStaticTestSuite destinationsuite)
        {

            ITestSuiteEntryCollection suiteentrys = sourcesuite.TestCases;
            destinationsuite.Entries.RemoveCases(destinationsuite.Entries.OfType<ITestCase>());

            foreach (ITestSuiteEntry testcase in suiteentrys)
            {
                try
                {   //check whether testcase exists in new work items(closed work items may not be created again).
                    if (!WorkItemIdMap.Contains(testcase.TestCase.WorkItem.Id))
                    {
                        continue;
                    }

                    int newWorkItemID = WorkItemIdMap[testcase.TestCase.WorkItem.Id];
                    ITestCase tc = destinationproj.TestCases.Find(newWorkItemID);
                    destinationsuite.Entries.Add(tc);

                    bool updateTestCase = false;
                    TestActionCollection testActionCollection = tc.Actions;
                    foreach (var item in testActionCollection)
                    {
                        var sharedStepRef = item as ISharedStepReference;
                        if (sharedStepRef != null)
                        {

                            int newSharedStepId = (int)WorkItemIdMap[sharedStepRef.SharedStepId];
                            //GetNewSharedStepId(testCase.Id, sharedStepRef.SharedStepId);
                            if (0 != newSharedStepId)
                            {
                                sharedStepRef.SharedStepId = newSharedStepId;
                                updateTestCase = true;
                            }

                        }
                    }
                    if (updateTestCase)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Test case with Id: {0} updated", tc.Id);
                        tc.Save();
                    }
                }
                catch (Exception)
                {
                    logger.Info("Error retrieving Test case  " + testcase.TestCase.WorkItem.Id + ": " + testcase.Title);
                }
            }
        }

    }


    
}
