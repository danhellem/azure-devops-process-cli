﻿using adoProcess.Helper;
using adoProcess.Helper.ConsoleTable;
using adoProcess.Models;
using adoProcess.ViewModels;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.ActivityStatistic;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace adoProcess
{
    public class Program
    {
        public static int Main(string[] args)
        {    

            if (args.Length == 0)
            {
                ShowUsage();
                return 0;
            }

            args = SetArgumentsFromConfig(args);

            string org, pat, process, project, refname, name, type, action;
            string witrefname, targetprocess;

            try
            {   
                CheckArguments(args, out org, out pat, out project, out refname, out name, out type, out action, out process, out witrefname, out targetprocess);     
                
                Uri baseUri = new Uri(org);

                VssCredentials clientCredentials = new VssCredentials(new VssBasicCredential("username", pat));
                VssConnection vssConnection = new VssConnection(baseUri, clientCredentials);

                if (action == "clonewit")                
                {                   
                    Console.WriteLine("Start Validation...");

                    bool val = CloneWitAndProcessValidation(vssConnection, process, targetprocess, witrefname);
                    
                    if (! val) return 0;
                }               

                //action out all fields
                if (action == "listallfields")
                {
                    var fields = Repos.Fields.GetAllFields(vssConnection);

                    var table = new ConsoleTable("Name", "Reference Name", "Type");

                    foreach (WorkItemField field in fields)
                    {
                        table.AddRow(field.Name, field.ReferenceName, field.Type);
                    }

                    table.Write();
                    Console.WriteLine();

                    return 0;
                }

                //get one field by refname and me all of the processes that field is in
                if (action == "getfield" && (! String.IsNullOrEmpty(refname)))
                {
                    List<ProcessInfo> processList = Repos.Process.GetProcesses(vssConnection);
                    List<ProcessWorkItemType> witList;                    

                    var table = new ConsoleTable("Process", "Work Item Type", "Field Name", "Field Reference Name");

                    foreach (var processInfo in processList)
                    {
                        witList = Repos.Process.GetWorkItemTypes(vssConnection, processInfo.TypeId);

                        foreach(var witItem in witList)
                        {
                            ProcessWorkItemTypeField witField = Repos.Process.GetField(vssConnection, processInfo.TypeId, witItem.ReferenceName, refname);

                            if (witField != null)
                            {
                                table.AddRow(processInfo.Name, witItem.Name, witField.Name, witField.ReferenceName);                             
                            }

                            witField = null;
                        }
                    }

                    table.Write();
                    Console.WriteLine();                                      
                }

                if (action == "getfieldforprojects" && (!String.IsNullOrEmpty(refname)))
                {
                    Console.WriteLine("Getting list of projects and work item types...");
                    Console.WriteLine();

                    var table = new ConsoleTable("Project", "Work Item Type", "Field Reference Name", "Field Name");
                    WorkItemTypeFieldWithReferences field;

                    List<TeamProjectReference> projectList = Repos.Projects.GetAllProjects(vssConnection);
                    List<WorkItemType> witList = null;

                    foreach (TeamProjectReference projectItem in projectList)
                    {                        
                        witList = Repos.WorkItemTypes.GetWorkItemTypesForProject(vssConnection, projectItem.Name);

                        foreach (WorkItemType witItem in witList)
                        {
                            field = Repos.Fields.GetFieldForWorkItemType(vssConnection, projectItem.Name, witItem.ReferenceName, refname);

                            if (field != null)
                            {
                                table.AddRow(projectItem.Name, witItem.ReferenceName, field.ReferenceName, field.Name);
                            }

                            field = null;
                        }
                    }                    

                    table.Write();
                    Console.WriteLine();

                    field = null;
                    table = null;
                    witList = null;
                }

                if (action == "searchfields")
                {
                    var fields = Repos.Fields.SearchFields(vssConnection, name, type);

                    if (fields.Count == 0)
                    {
                        Console.WriteLine("No fields found for name: '" + name + "' or type: '" + type + "'");
                        return 0;
                    }

                    var table = new ConsoleTable("Name", "Reference Name", "Type");

                    foreach (WorkItemField field in fields)
                    {
                        table.AddRow(field.Name, field.ReferenceName, field.Type);
                    }

                    table.Write();
                    Console.WriteLine();

                    return 0;
                }               

                //add new field to the organization
                if (action == "addfield")
                {
                    //check to see if the type is a legit type
                    int pos = Array.IndexOf(Repos.Fields.Types, type);

                    if (pos == -1)
                    {
                        var types = Repos.Fields.Types;

                        Console.WriteLine("Invalid field type value '" + type + "'");
                        Console.WriteLine();
                        Console.WriteLine("Valid field types are:");
                        Console.WriteLine();

                        foreach (string item in types)
                        {
                            Console.WriteLine(item);
                        }

                        return 0;
                    }

                    //check and make sure the field does not yet exist
                    var field = Repos.Fields.GetField(vssConnection, refname);

                    if (field != null)
                    {
                        Console.WriteLine("Field already exists");
                        Console.WriteLine();

                        var table = new ConsoleTable("Name", "Reference Name", "Type");

                        table.AddRow(field.Name, field.ReferenceName, field.Type);

                        table.Write();
                        Console.WriteLine();

                        return 0;
                    }
                                       
                    WorkItemField newField = Repos.Fields.AddField(vssConnection, refname, name, type);

                    if (newField != null)
                    {
                        Console.WriteLine("Field '" + refname + "' was successfully added");
                    }
                }

                if (action == "listfieldsforprocess")
                {
                    List<FieldsPerProcess> list = Repos.Fields.ListFieldsForProcess(vssConnection, process);

                    var table = new ConsoleTable("Work Item Type", "Name", "Reference Name", "Type");

                    foreach (FieldsPerProcess item in list)
                    {
                        List<ProcessWorkItemTypeField> fields = item.fields;

                        foreach(ProcessWorkItemTypeField field in fields)
                        {
                            table.AddRow(item.workItemType.Name, field.Name, field.ReferenceName, field.Type);
                        }                      
                    }

                    table.Write();
                    Console.WriteLine();

                    return 0;
                }

                vssConnection = null;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);

                ShowUsage();
                return -1;
            }

            return 0;        
        }
         
        private static bool CloneWitAndProcessValidation(VssConnection vssConnection, string process, string targetProcess, string witRefName)
        {
            List<ProcessInfo> processList = Repos.Process.GetProcesses(vssConnection);

            ProcessInfo sourceProcessInfo = processList.Find(x => x.Name == process);
            ProcessInfo targetProcessInfo = processList.Find(x => x.Name == targetProcess);

            Console.Write("  Validating source process '{0}'...", process);

            if (sourceProcessInfo == null)
            {
                Console.Write("failed (process not found) \n");
                return false;
            }
            else
            {
                Console.Write("done \n");
            };

            Console.Write("  Validating target process '{0}'...", targetProcess);

            if (targetProcessInfo == null)
            {
                Console.Write("failed (process not found) \n");
                return false;
            }
            else
            {
                Console.Write("done \n");
            };

            Console.Write("  Validating work item type '{0}' exists in source process...", witRefName);

            if (Repos.Process.GetWorkItemType(vssConnection, sourceProcessInfo.TypeId, witRefName) == null)
            {
                Console.Write("failed (work item type not found) \n");
                return false;
            }
            else
            {
                Console.Write("done \n");
            }

            Console.Write("  Validating work item type {0} does not exist in target process...", witRefName);

            if (Repos.Process.GetWorkItemType(vssConnection, targetProcessInfo.TypeId, witRefName) != null)
            {
                Console.Write("failed (work item type found) \n");
                return false;
            }
            else
            {
                Console.Write("done \n");
            }

            return true;
        }

        private static void CheckArguments(string[] args, out string org, out string pat, out string project, out string refname, out string name, out string type, out string action, out string process, out string witrefname, out string targetprocess)
        {
            org = null;
            refname = null;
            name = null;
            type = null;
            action = null;
            project = null;
            pat = null;
            process = null;
            witrefname = null;
            targetprocess = null;

            //Dictionary<string, string> argsMap = new Dictionary<string, string>();
            
            foreach (var arg in args)
            {
                if (arg[0] == '/' && arg.IndexOf(':') > 1)
                {
                    string key = arg.Substring(1, arg.IndexOf(':') - 1);
                    string value = arg.Substring(arg.IndexOf(':') + 1);

                    switch (key)
                    {
                        case "org":
                            org = "https://dev.azure.com/" + value;
                            break;
                        case "pat":
                            pat = value;
                            break;
                        case "project":
                            project = value;
                            break;
                        case "refname":
                            refname = value;
                            break;
                        case "name":
                            name = value;
                            break;
                        case "type":
                            type = value;
                            break;
                        case "action":
                            action = value;
                            break;
                        case "process":
                            process = value;
                            break;
                        case "witrefname":
                            witrefname = value;
                            break;
                        case "targetprocess":
                            targetprocess = value;
                            break;
                        default:
                            throw new ArgumentException("Unknown argument", key);
                    }
                }
            }           

            if (org == null || pat == null)
            {
                throw new ArgumentException("Missing required arguments");
            }
            
            if ((action == "getfield") && string.IsNullOrEmpty(refname))
            {
                throw new ArgumentException("getfield action requires refname value");
            }

            if ((action == "getfieldforprojects") && string.IsNullOrEmpty(refname))
            {
                throw new ArgumentException("getfieldforprojects action requires field refname value");
            }

            if ((action == "addfield") && (string.IsNullOrEmpty(refname) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type)))
            {
                throw new ArgumentException("addfield action requires refname, name, and type value");
            }

            if ((action == "searchfield" && string.IsNullOrEmpty(name) && string.IsNullOrEmpty(refname)))
            {
                 throw new ArgumentException("searchfield action requires name or type value");
            }    
            
            if (action == "listfieldsforprocess" && string.IsNullOrEmpty(process))
            {
                throw new ArgumentException("listfieldsforprocess action requires process");
            }

            if (action == "clonewit")            {                

                if (process == null)
                {
                    throw new ArgumentException("Missing required argument 'process'");
                }

                if (witrefname == null)
                {
                    throw new ArgumentException("Missing required argument 'witrefname' for the work item type you want to clone");
                }

                if (targetprocess == null)
                {
                    throw new ArgumentException("Missing required argument 'targetprocess' for the process you want to clone the work item type into");
                }
            }           
        }        

        private static string[] SetArgumentsFromConfig (string[] args)
        {
            var configHelper = new ConfigHelper();
            bool org = false;
            bool pat = false;
           
            foreach (var arg in args)
            {
                if (arg[0] == '/' && arg.IndexOf(':') > 1)
                {
                    string key = arg.Substring(1, arg.IndexOf(':') - 1);
                    string value = arg.Substring(arg.IndexOf(':') + 1);

                    switch (key)
                    {
                        case "org":
                            org = true;
                            break;
                        case "pat":
                            pat = true;
                            break;
                        default:
                            break;
                    }
                }
            }

            if (! org && !String.IsNullOrEmpty(configHelper.Organization))
            {
                Array.Resize(ref args, args.Length + 1);
                args[args.Length - 1] = "/org:" + configHelper.Organization;
            }

            if (!pat && !String.IsNullOrEmpty(configHelper.PersonalAccessToken))
            {
                Array.Resize(ref args, args.Length + 1);
                args[args.Length - 1] = "/pat:" + configHelper.PersonalAccessToken;
            }

            configHelper = null;

            return args;
        }

        private static void ShowUsage()
        {
            Console.WriteLine("CLI to manage an inherited process in Azure DevOps");
            Console.WriteLine("");
            Console.WriteLine("Arguments:");
            Console.WriteLine("");
            Console.WriteLine("  /org:{value}               azure devops organization name");
            Console.WriteLine("  /pat:{value}               personal access token");
            Console.WriteLine("");
            Console.WriteLine("  /action:                   listallfields, getfieldforprojects, addfield, searchfield, listfieldsforprocess, clonewit");
            Console.WriteLine("  /refname:{value}           refname of field getting or adding");
            Console.WriteLine("  /name:{value}              field friendly name");
            Console.WriteLine("  /process:{value}           name of process");
            Console.WriteLine("  /type:{value}              type field creating"); 
            Console.WriteLine("  /targetprocess:{value}     target process for where you want to clone a wit into");
          
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("");
            Console.WriteLine("  /org:fabrikam /pat:{value} /action:listallfields");
            Console.WriteLine("  /org:fabrikam /pat:{value} /action:getfield /refname:System.Title");
            Console.WriteLine("  /org:fabrikam /pat:{value} /action:listfieldsforprocess /process:Agile");
            Console.WriteLine("  /org:fabrikam /pat:{value} /action:clonewit /process:sourceprocess /witrefname:custom.ticket /targetprocess:targetprocess");

            Console.WriteLine("");
        }
    }
}
