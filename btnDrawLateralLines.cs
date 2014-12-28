using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DrawLateralLines
{
    public class btnDrawLateralLines : ESRI.ArcGIS.Desktop.AddIns.Button
    {
        public btnDrawLateralLines()
        {
        }

        protected override void OnClick()
        {
            frmDrawLateralLines frm = new frmDrawLateralLines();
            frm.ShowDialog();
            ArcMap.Application.CurrentTool = null;
        }
        protected override void OnUpdate()
        {
            Enabled = ArcMap.Application != null;
        }
    }

}
