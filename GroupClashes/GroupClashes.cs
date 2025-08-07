using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api.Plugins;

namespace GroupClashes
{
    [Plugin("GroupClashes", "BM42", DisplayName = "Group Clashes")]
    [Strings("GroupClashes.name")]
    [RibbonLayout("GroupClashes.xaml")]
    [RibbonTab("ID_GroupClashesTab",
        DisplayName = "Group Clashes")]
    [Command("ID_GroupClashesButton",
             Icon = "GroupClashesIcon_Small.ico", LargeIcon = "GroupClashesIcon_Large.ico",
             DisplayName = "Group Clashes")]

    class RibbonHandler : CommandHandlerPlugin
    {
        public RibbonHandler()
        {
            Logger.Initialize();
            Logger.LogInfo("RibbonHandler constructor called");
        }

        public override int ExecuteCommand(string commandId, params string[] parameters)
        {
            Logger.LogUserAction("Ribbon Command Executed", $"CommandId: {commandId}");
            
            try
            {
                if (Autodesk.Navisworks.Api.Application.IsAutomated)
                {
                    Logger.LogWarning("Attempted to run in automation mode");
                    throw new InvalidOperationException("Invalid when running using Automation");
                }

                Logger.LogInfo("Looking for GroupClashes plugin");
                
                //Find the plugin
                PluginRecord pr = Autodesk.Navisworks.Api.Application.Plugins.FindPlugin("GroupClashes.GroupClashesPane.BM42");

                if (pr != null && pr is DockPanePluginRecord && pr.IsEnabled)
                {
                    Logger.LogInfo($"Plugin found: Enabled={pr.IsEnabled}, Loaded={pr.LoadedPlugin != null}");
                    
                    //check if it needs loading
                    if (pr.LoadedPlugin == null)
                    {
                        Logger.LogInfo("Loading plugin for first time");
                        pr.LoadPlugin();
                    }

                    DockPanePlugin dpp = pr.LoadedPlugin as DockPanePlugin;
                    if (dpp != null)
                    {
                        Logger.LogInfo($"Toggling plugin visibility: Current={dpp.Visible}");
                        //switch the Visible flag
                        dpp.Visible = !dpp.Visible;
                        Logger.LogInfo($"Plugin visibility changed to: {dpp.Visible}");
                    }
                    else
                    {
                        Logger.LogError("Failed to cast loaded plugin to DockPanePlugin");
                    }
                }
                else
                {
                    Logger.LogError($"Plugin not found or not enabled: Found={pr != null}, Enabled={pr?.IsEnabled}");
                }

                Logger.LogInfo("Command execution completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing ribbon command", ex);
                throw;
            }
        }

        public override CommandState CanExecuteCommand(String commandId)
        {
            CommandState state = new CommandState();
            state.IsVisible = true;
            state.IsEnabled = true;
            state.IsChecked = true;

            return state;
        }

        public override bool CanExecuteRibbonTab(string name)
        {
            return true;
        }

        public override bool TryShowCommandHelp(string name)
        {
            //FileInfo dllFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            //string pathToHtmlFile = Path.Combine(dllFileInfo.Directory.FullName, @"Help\Help.html");
            string helpUrl = @"https://witty-river-01a861010.2.azurestaticapps.net/GroupClashes/GroupClashes.html";
            System.Diagnostics.Process.Start(helpUrl);
            return true;
        }


    }
}


