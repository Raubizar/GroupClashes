using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;

namespace GroupClashes
{
    [Plugin("GroupClashes.GroupClashesPane", "BM42",DisplayName = "Group Clashes",ToolTip = "Group clashes")]
    [DockPanePlugin(300, 380)]
    class GroupClashesPane : DockPanePlugin
    {
        public override Control CreateControlPane()
        {
            Logger.LogInfo("Creating control pane for GroupClashes");
            
            try
            {
                //create the control that will be used to display in the pane
                GroupClashesHostingControl control = new GroupClashesHostingControl();

                control.Dock = DockStyle.Fill;

                //create the control
                control.CreateControl();
                
                Logger.LogInfo("Control pane created successfully");
                return control;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error creating control pane", ex);
                throw;
            }
        }

        public override void DestroyControlPane(Control pane)
        {
            Logger.LogInfo("Destroying control pane for GroupClashes");
            
            try
            {
                pane.Dispose();
                Logger.LogSessionEnd();
                Logger.LogInfo("Control pane disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error disposing control pane", ex);
            }
        }
    }
}
