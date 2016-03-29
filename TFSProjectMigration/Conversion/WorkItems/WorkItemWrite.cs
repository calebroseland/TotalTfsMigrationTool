using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System.Xml;
using System.IO;
using log4net;
using System.Windows.Controls;
using TFSProjectMigration.Conversion.WorkItems;
using TFSProjectMigration.Conversion.ProjectStructure;

namespace TFSProjectMigration
{
   public class WorkItemWrite
    {
        TfsTeamProjectCollection tfs;
        public WorkItemStore store;
        Project destinationProject;

        WorkItemTypeCollection workItemTypes;
        public WorkItemIdMap WorkItemIdMap { get; set; }
        public AreaIdMap AreadIdMap { get; set; }
        public IterationIdMap IterationIdMap { get; set; }

         //public Hashtable itemMap;
         //public Hashtable fieldMapAll;
         //public Hashtable itemMapCIC;
         private static readonly ILog logger = LogManager.GetLogger(typeof(TFSWorkItemMigrationUI));
        bool CorrectErrorsAbsolutely = true;


        public WorkItemWrite(TfsTeamProjectCollection tfs, Project destinationProject)
        {
            this.tfs = tfs;
            this.destinationProject = destinationProject;
            store = (WorkItemStore)tfs.GetService(typeof(WorkItemStore));

            workItemTypes = store.Projects[destinationProject.Name].WorkItemTypes;
            WorkItemIdMap = new WorkItemIdMap();
        }

        //get all workitems from tfs
        private WorkItemCollection GetWorkItemCollection()
        {
            WorkItemCollection workItemCollection = store.Query(" SELECT * " +
                                                                  " FROM WorkItems " +
                                                                  " WHERE [System.TeamProject] = '" + destinationProject.Name +
                                                                  "' ORDER BY [System.Id]");
            return workItemCollection;
        }


        public void updateToLatestStatus(WorkItem oldWorkItem, WorkItem newWorkItem)
        {
            Queue<string> result = new Queue<string>();
            string previousState = null;
            string originalState = (string)newWorkItem.Fields["State"].Value;
            string sourceState = (string)oldWorkItem.Fields["State"].Value;
            string sourceFinalReason = (string)oldWorkItem.Fields["Reason"].Value;

            //try to change the status directly
            newWorkItem.Open();
            newWorkItem.Fields["State"].Value = oldWorkItem.Fields["State"].Value;
            //System.Diagnostics.Debug.WriteLine(newWorkItem.Type.Name + "      " + newWorkItem.Fields["State"].Value);

            //if status can't be changed directly... 
            if (newWorkItem.Fields["State"].Status != FieldStatus.Valid)
            {
                //get the state transition history of the source work item.
                foreach (Revision revision in oldWorkItem.Revisions)
                {
                    // Get Status          
                    if (!revision.Fields["State"].Value.Equals(previousState))
                    {
                        previousState = revision.Fields["State"].Value.ToString();
                        result.Enqueue(previousState);
                    }

                }

                int i = 1;
                previousState = originalState;
                //traverse new work item through old work items's transition states
                foreach (String currentStatus in result)
                {
                    bool success = false;
                    if (i != result.Count)
                    {
                        success = ChangeWorkItemStatus(newWorkItem, previousState, currentStatus);
                        previousState = currentStatus;
                    }
                    else
                    {
                        success = ChangeWorkItemStatus(newWorkItem, previousState, currentStatus, sourceFinalReason);
                    }
                    i++;
                    // If we could not do the incremental state change then we are done.  We will have to go back to the orginal...
                    if (!success)
                        break;
                }
            }
            else
            {
                // Just save it off if we can.
                bool success = ChangeWorkItemStatus(newWorkItem, originalState, sourceState);
            }
        }

        private bool ChangeWorkItemStatus(WorkItem workItem, string orginalSourceState, string destState, string reason = null)
        {
            //Try to save the new state.  If that fails then we also go back to the orginal state.
            try
            {
                workItem.Open();
                workItem.Fields["State"].Value = destState;
                if(reason != null)
                    workItem.Fields["Reason"].Value = reason;
                
                var errors = workItem.Validate();
                foreach (Field item in errors)
                {
                    if (!CorrectErrorsAbsolutely)
                    {
                        logger.WarnFormat("Work item {0} Validation Error in field: {1}  : {2}", workItem.Id,
                            item.Name, workItem.Fields[item.Name].Value);
                    }
                    else
                    {
                        var initialValue = FindBestState(item.Value.ToString(), item.AllowedValues);
                        var allowedValueSortedByAcceptability =
                            item.AllowedValues.OfType<string>().OrderBy(e => LevenshteinDistance.Compute(initialValue, e));
                        var value = allowedValueSortedByAcceptability.First();
                        item.Value = value;
                        logger.WarnFormat("Work item {0} Validation Error in field: {1}  : {2}=> Replaced by value: {3}", workItem.Id,
                            item.Name, destState, value);
                    }
                }
                workItem.Save();
                return true;
            }
            catch (Exception)
            {
                logger.WarnFormat("Failed to save state for workItem: {0}  type:'{1}' state from '{2}' to '{3}' => rolling workItem status to original state '{4}'",
                    workItem.Id, workItem.Type.Name, orginalSourceState, destState, orginalSourceState);
                //Revert back to the original value.
                workItem.Fields["State"].Value = orginalSourceState;
                return false;
            }
        }

        private string FindBestState(string currentValue, AllowedValuesCollection allowedValues)
        {
            switch (currentValue)
            {
                case "Fermé":
                    if (allowedValues.Contains("Closed"))
                        return "Closed";
                    return currentValue;
                case "Actif":
                    if (allowedValues.Contains("Active"))
                        return "Active";
                    return currentValue;
                default:
                    return currentValue;
            }
        }

        /* Copy work items to project from work item collection */
        public void writeWorkItems(WorkItemStore sourceStore, WorkItemCollection workItemCollection, string sourceProjectName, WorkItemTypeMap witMap)
        {
            //ReadItemMap(sourceProjectName);
            int i = 1;
            List<WorkItem> newItems = new List<WorkItem>();
            foreach (WorkItem sourceWorkItem in workItemCollection)
            {
                if (WorkItemIdMap.Contains(sourceWorkItem.Id))
                {
                    //already copied
                    continue;
                }

               var map = witMap.GetMapping(sourceWorkItem.Type);
               if (map == null)
               {
                  logger.InfoFormat("Work Item Type {0} is not mapped", sourceWorkItem.Type.Name);
                  continue;
               }

               var fieldMap = witMap.GetFieldMapping(sourceWorkItem.Type, map);
               var newWorkItem = new WorkItem(map);

                /* assign relevent fields*/
                foreach (Field field in sourceWorkItem.Fields)
                {
                     //these fields can't be updated
                    if (field.ReferenceName == "System.Id" 
                        || field.ReferenceName == "System.State" 
                        || field.ReferenceName == "System.Reason" 
                        || field.ReferenceName == "System.Rev"
                        || field.ReferenceName == "System.Watermark"
                        || field.ReferenceName == "System.AreaId"
                        || field.ReferenceName == "System.IterationId")
                    {
                        // ignore
                        continue;
                    }

                    //Add values to mapped fields
                    if (fieldMap.ContainsKey(field.FieldDefinition))
                    {
                        try
                        {
                           var mappedField = fieldMap[field.FieldDefinition];
                           if (!newWorkItem.Fields[mappedField.Name].IsEditable)
                           {
                              //readonly, try again later
                              logger.Warn("Field readonly: " + mappedField.ReferenceName);
                              continue;
                           }

                            if (field.Name.Contains("Closed"))
                           {

                           }

                           if (field.ReferenceName == "System.AreaPath"
                                       || field.ReferenceName == "System.IterationPath"
                                       || field.ReferenceName == "System.TeamProject")
                           {
                              try
                              {
                                 string iterationPath = (string)field.Value;
                                 int length = sourceProjectName.Length;

                                 string itPathNew = destinationProject.Name + iterationPath.Substring(length);
                                 newWorkItem.Fields[mappedField.Name].Value = itPathNew;
                              }
                              catch (Exception ex)
                              {
                                 logger.Warn("Error supressed: ", ex);
                              }
                           }
                           else
                           {
                              newWorkItem.Fields[mappedField.Name].Value = field.Value;
                           }
                        }
                        catch (Exception ex)
                        {
                           logger.Warn("Error in set value: ", ex);
                        }
                       
                    }
                }

                /* Validate Item Before Save*/
                ArrayList array = newWorkItem.Validate();
                bool isInError = array.Count != 0;
                foreach (Field item in array)
                {
                    if (!CorrectErrorsAbsolutely)
                    {
                        logger.WarnFormat("Work item {0} Validation Error in field: {1}  : {2}", sourceWorkItem.Id,
                            item.Name, newWorkItem.Fields[item.Name].Value);
                    }
                    else
                    {
                        var initialValue = item.Value.ToString();
                        var allowedValueSortedByAcceptability =
                            item.AllowedValues.OfType<string>().OrderBy(e => LevenshteinDistance.Compute(initialValue, e));
                        var value = allowedValueSortedByAcceptability.First();
                        item.Value = value;
                        logger.WarnFormat("Work item {0} Validation Error in field: {1}  : {2}=> Replaced by value: {3}", sourceWorkItem.Id,
                            item.Name, initialValue, value);
                    }
                }
                if (isInError && CorrectErrorsAbsolutely)
                    array = newWorkItem.Validate();

                //if work item is valid
                if (array.Count == 0)
                {
                    try
                    {
                        UploadAttachments(newWorkItem, sourceWorkItem);
                        newWorkItem.Save();

                        WorkItemIdMap.Map(sourceWorkItem.Id, newWorkItem.Id);
                        newItems.Add(sourceWorkItem);
                    }
                    catch (Exception)
                    {
                        throw;
                    }

                    //update workitem status
                    updateToLatestStatus(sourceWorkItem, newWorkItem);
                }
                else
                {
                    logger.ErrorFormat("Work item {0} could not be saved", sourceWorkItem.Id);
                }

                //ProgressBar.Dispatcher.BeginInvoke(new Action(delegate()
                //{
                //    float progress = (float)i / (float)workItemCollection.Count;
                //    ProgressBar.Value = ((float)i / (float)workItemCollection.Count) * 100;
                //}));
                i++;
            }

            //WriteMaptoFile(sourceProjectName);
            CreateLinks(newItems, sourceStore);
        }


        static class LevenshteinDistance
        {
            public static int Compute(string s, string t)
            {
                if (string.IsNullOrEmpty(s))
                {
                    if (string.IsNullOrEmpty(t))
                        return 0;
                    return t.Length;
                }

                if (string.IsNullOrEmpty(t))
                {
                    return s.Length;
                }

                int n = s.Length;
                int m = t.Length;
                int[,] d = new int[n + 1, m + 1];

                // initialize the top and right of the table to 0, 1, 2, ...
                for (int i = 0; i <= n; d[i, 0] = i++) ;
                for (int j = 1; j <= m; d[0, j] = j++) ;

                for (int i = 1; i <= n; i++)
                {
                    for (int j = 1; j <= m; j++)
                    {
                        int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                        int min1 = d[i - 1, j] + 1;
                        int min2 = d[i, j - 1] + 1;
                        int min3 = d[i - 1, j - 1] + cost;
                        d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                    }
                }
                return d[n, m];
            }
        }


        private Hashtable ListToTable(List<object> map)
        {
            Hashtable table = new Hashtable();
            if (map != null)
            {
                foreach (object[] item in map)
                {
                    try {
	                    table.Add((string)item[0], (string)item[1]);
                    } catch (Exception ex) {
                    	logger.Error("Error in ListToTable", ex);
                    }
                }
            }
            return table;
        }

        //private void ReadItemMap(string sourceProjectName)
        //{
        //    string filaPath = String.Format(@"Map\ID_map_{0}_to_{1}.txt", sourceProjectName, destinationProject.Name);
        //    itemMap = new Hashtable();
        //    string line;
        //    if (File.Exists(filaPath))
        //    {
        //        System.IO.StreamReader file = new System.IO.StreamReader(filaPath);
        //        while ((line = file.ReadLine()) != null)
        //        {
        //            if (line.Contains("Source ID|Target ID"))
        //            {
        //                continue;
        //            }
        //            string[] idMap = line.Split(new char[] { '|' });
        //            if (idMap[0].Trim() != "" && idMap[1].Trim() != "")
        //            {
        //                itemMap.Map(Convert.ToInt32(idMap[0].Trim()), Convert.ToInt32(idMap[1].Trim()));
        //            }
        //        }
        //        file.Close();
        //    }
        //}

        /* Set links between workitems */
        private void CreateLinks(List<WorkItem> workItemCollection, WorkItemStore sourceStore)
        {
            List<int> linkedWorkItemList = new List<int>();
            WorkItemCollection targetWorkItemCollection = GetWorkItemCollection();
            foreach (WorkItem workItem in workItemCollection)
            {
                WorkItemLinkCollection links = workItem.WorkItemLinks;
                if (links.Count > 0)
                {
                    int newWorkItemID = (int)WorkItemIdMap[workItem.Id];
                    WorkItem newWorkItem = store.GetWorkItem(newWorkItemID);

                    foreach (WorkItemLink link in links)
                    {
                        try
                        {
                            WorkItem targetItem = sourceStore.GetWorkItem(link.TargetId);
                            if (WorkItemIdMap.Contains(link.TargetId)  && targetItem != null)
                            {

                                int targetWorkItemID = 0;
                                if (WorkItemIdMap.Contains(link.TargetId))
                                {
                                    targetWorkItemID = (int)WorkItemIdMap[link.TargetId];
                                }
                                
                                //if the link is not already created(check if target id is not in list)
                                if (!linkedWorkItemList.Contains(link.TargetId))
                                {
                                    try
                                    {
                                        WorkItemLinkTypeEnd linkTypeEnd = store.WorkItemLinkTypes.LinkTypeEnds[link.LinkTypeEnd.Name];
                                        newWorkItem.Links.Add(new RelatedLink(linkTypeEnd, targetWorkItemID));

                                        ArrayList array = newWorkItem.Validate();
                                        if (array.Count == 0)
                                        {
                                            newWorkItem.Save();
                                        }
                                        else
                                        {
                                            logger.Info("WorkItem Validation failed at link setup for work item: " + workItem.Id);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.ErrorFormat("Error occured when crearting link for work item: {0} target item: {1}", workItem.Id, link.TargetId);
                                        logger.Error("Error detail", ex);
                                    }

                                }
                            }
                            else
                            {
                                logger.Info("Link is not created for work item: " + workItem.Id + " - target item: " + link.TargetId + " does not exist");
                            }
                        }
                        catch (Exception)
                        {
                            logger.Warn("Link is not created for work item: " + workItem.Id + " - target item: " + link.TargetId + " is not in Source TFS or you do not have permission to access");
                        }
                    }
                    //add the work item to list if the links are processed
                    linkedWorkItemList.Add(workItem.Id);

                }
            }
        }

        /* Upload attachments to workitems from local folder */
        private void UploadAttachments(WorkItem workItem, WorkItem workItemOld)
        {
            AttachmentCollection attachmentCollection = workItemOld.Attachments;
            foreach (Attachment att in attachmentCollection)
            {
                string comment = att.Comment;
                string name = @"Attachments\" + workItemOld.Id + "\\" + att.Name;
                string nameWithID = @"Attachments\" + workItemOld.Id + "\\" + att.Id + "_" + att.Name;
                try
                {
                    if (File.Exists(nameWithID))
                    {
                        workItem.Attachments.Add(new Attachment(nameWithID, comment));
                    }
                    else
                    {
                        workItem.Attachments.Add(new Attachment(name, comment));
                    }
                }
                catch (Exception ex)
                {
                    logger.ErrorFormat("Error saving attachment: {0} for workitem: {1}", att.Name, workItemOld.Id);
                    logger.Error("Error detail: ", ex);
                }
            }
        }




        /* write ID mapping to local file */
        //public void WriteMaptoFile(string sourceProjectName)
        //{
        //    string filaPath = String.Format(@"Map\ID_map_{0}_to_{1}.txt", sourceProjectName, destinationProject.Name);
        //    if (!Directory.Exists(@"Map"))
        //    {
        //        Directory.CreateDirectory(@"Map");
        //    }
        //    else if (File.Exists(filaPath))
        //    {
        //        System.IO.File.WriteAllText(filaPath, string.Empty);
        //    }

        //    using (System.IO.StreamWriter file = new System.IO.StreamWriter(filaPath, false))
        //    {
        //        file.WriteLine("Source ID|Target ID");
        //        foreach (object key in itemMap)
        //        {
        //            DictionaryEntry item = (DictionaryEntry)key;
        //            file.WriteLine(item.Key + "\t | \t" + item.Value);
        //        }
        //    }

        //}


        //Delete all workitems in project
        public void DeleteWorkItems()
        {
            WorkItemCollection workItemCollection = GetWorkItemCollection();
            List<int> toDeletes = new List<int>();

            foreach (WorkItem workItem in workItemCollection)
            {
                System.Diagnostics.Debug.WriteLine(workItem.Id);
                toDeletes.Add(workItem.Id);
            }
            var errors = store.DestroyWorkItems(toDeletes);
            foreach (var error in errors)
            {
                System.Diagnostics.Debug.WriteLine(error.Exception.Message);
            }

        }

        /* Compare work item type definitions and add fields from source work item types and replace workflow */
        public void SetFieldDefinitions(WorkItemTypeCollection workItemTypesSource, Hashtable fieldList)
        {
            foreach (WorkItemType workItemTypeSource in workItemTypesSource)
            {
                WorkItemType workItemTypeTarget = null;
                if (workItemTypeSource.Name == "User Story")
                {
                    workItemTypeTarget = workItemTypes["Product Backlog Item"];
                }
                else if (workItemTypeSource.Name == "Issue")
                {
                    workItemTypeTarget = workItemTypes["Impediment"];
                }
                else
                {
                    workItemTypeTarget = workItemTypes[workItemTypeSource.Name];
                }

                XmlDocument workItemTypeXmlSource = workItemTypeSource.Export(false);
                XmlDocument workItemTypeXmlTarget = workItemTypeTarget.Export(false);

                workItemTypeXmlTarget = AddNewFields(workItemTypeXmlSource, workItemTypeXmlTarget, (List<object>)fieldList[workItemTypeTarget.Name]);

                try
                {
                    WorkItemType.Validate(destinationProject, workItemTypeXmlTarget.InnerXml);
                    destinationProject.WorkItemTypes.Import(workItemTypeXmlTarget.InnerXml);
                }
                catch (XmlException)
                {
                    logger.Info("XML import falied for " + workItemTypeSource.Name);
                }

            }

        }

        /* Add field definitions from Source xml to target xml */
        private XmlDocument AddNewFields(XmlDocument workItemTypeXmlSource, XmlDocument workItemTypeXmlTarget, List<object> fieldList)
        {
            XmlNodeList parentNodeList = workItemTypeXmlTarget.GetElementsByTagName("FIELDS");
            XmlNode parentNode = parentNodeList[0];
            foreach (object[] list in fieldList)
            {
                if ((bool)list[1])
                {
                    XmlNodeList transitionsListSource = workItemTypeXmlSource.SelectNodes("//FIELD[@name='" + list[0] + "']");
                    try
                    {
                        XmlNode copiedNode = workItemTypeXmlTarget.ImportNode(transitionsListSource[0], true);
                        parentNode.AppendChild(copiedNode);
                    }
                    catch (Exception)
                    {
                        logger.ErrorFormat("Error adding new field for parent node : {0}", parentNode.Value);
                    }
                }
            }
            return workItemTypeXmlTarget;
        }


        /*Add new Field definition to work item type */
        private XmlDocument AddField(XmlDocument workItemTypeXml, string fieldName, string fieldRefName, string fieldType, string fieldReportable)
        {
            XmlNodeList tempList = workItemTypeXml.SelectNodes("//FIELD[@name='" + fieldName + "']");
            if (tempList.Count == 0)
            {
                XmlNode parent = workItemTypeXml.GetElementsByTagName("FIELDS")[0];
                XmlElement node = workItemTypeXml.CreateElement("FIELD");
                node.SetAttribute("name", fieldName);
                node.SetAttribute("refname", fieldRefName);
                node.SetAttribute("type", fieldType);
                node.SetAttribute("reportable", fieldReportable);
                parent.AppendChild(node);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Field already exists...");
                logger.InfoFormat("Field {0} already exists", fieldName);
            }
            return workItemTypeXml;
        }


        public string ReplaceWorkFlow(WorkItemTypeCollection workItemTypesSource, List<object> fieldList)
        {
            string error = "";
            for (int i = 0; i < fieldList.Count; i++)
            {
                object[] list = (object[])fieldList[i];
                if ((bool)list[1])
                {
                    WorkItemType workItemTypeTarget = workItemTypes[(string)list[0]];

                    WorkItemType workItemTypeSource = null;
                    if (workItemTypesSource.Contains((string)list[0]))
                    {
                        workItemTypeSource = workItemTypesSource[(string)list[0]];
                    }
                    else if (workItemTypeTarget.Name == "Product Backlog Item")
                    {
                        workItemTypeSource = workItemTypesSource["User Story"];
                    }
                    else if (workItemTypeTarget.Name == "Impediment")
                    {
                        workItemTypeSource = workItemTypesSource["Issue"];
                    }

                    XmlDocument workItemTypeXmlSource = workItemTypeSource.Export(false);
                    XmlDocument workItemTypeXmlTarget = workItemTypeTarget.Export(false);

                    XmlNodeList transitionsListSource = workItemTypeXmlSource.GetElementsByTagName("WORKFLOW");
                    XmlNode transitions = transitionsListSource[0];

                    XmlNodeList transitionsListTarget = workItemTypeXmlTarget.GetElementsByTagName("WORKFLOW");
                    XmlNode transitionsTarget = transitionsListTarget[0];
                    string defTarget = "";
                    try
                    {
                        string def = workItemTypeXmlTarget.InnerXml;
                        string workflowSource = transitions.OuterXml;
                        string workflowTarget = transitionsTarget.OuterXml;

                        defTarget = def.Replace(workflowTarget, workflowSource);
                        WorkItemType.Validate(destinationProject, defTarget);
                        destinationProject.WorkItemTypes.Import(defTarget);
                        fieldList.Remove(list);
                        i--;
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error Replacing work flow");
                        error = error + "Error Replacing work flow for " + (string)list[0] + ":" + ex.Message + "\n";
                    }

                }
            }
            return error;
        }


        private object[] GetAllTransitionsForWorkItemType(XmlDocument workItemTypeXml)
        {
            XmlNodeList transitionsList = workItemTypeXml.GetElementsByTagName("TRANSITION");

            string[] start = new string[transitionsList.Count];
            string[] dest = new string[transitionsList.Count];
            string[][] values = new string[transitionsList.Count][];

            int j = 0;
            foreach (XmlNode transition in transitionsList)
            {
                start[j] = transition.Attributes["from"].Value;
                dest[j] = transition.Attributes["to"].Value;

                XmlNodeList reasons = transition.SelectNodes("REASONS/REASON");

                string[] reasonVal = new string[1 + reasons.Count];
                reasonVal[0] = transition.SelectSingleNode("REASONS/DEFAULTREASON").Attributes["value"].Value;

                int i = 1;
                if (reasons != null)
                {
                    foreach (XmlNode reason in reasons)
                    {
                        reasonVal[i] = reason.Attributes["value"].Value;
                        i++;
                    }
                }
                values[j] = reasonVal;
                j++;
            }

            return new object[] { start, dest, values };
        }





    }
}
