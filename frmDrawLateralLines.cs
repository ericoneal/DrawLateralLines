using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.ArcMapUI;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Catalog;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Framework;
using ESRI.ArcGIS.Display;
using System.Collections.ObjectModel;

namespace DrawLateralLines
{
    public partial class frmDrawLateralLines : Form
    {

        //IStepProgressor pStepProgressor;
          IGeometry pGeom;
            ITopologicalOperator topoOperator;
            IGeometry buffer;
            IFeatureIndex pFeatureIndex;
            ISpatialFilter spatialFilter;
            IFeatureCursor featureCursor;
            IFeature pFeature;
            IPoint ppointEnd;
            IProximityOperator pProximityOperator;

        public frmDrawLateralLines()
        {
            InitializeComponent();
        }

        private void frmDrawLateralLines_Load(object sender, EventArgs e)
        {
            FillCombos();
        }


        private void FillCombos()
        {
            IMxDocument pmxdoc = ArcMap.Document as IMxDocument;
            IMap pmap = pmxdoc.FocusMap;
            IFeatureLayer player;
            for (int i = 0; i <= pmap.LayerCount - 1; i++)
            {
                player = pmap.get_Layer(i) as IFeatureLayer;


                if (player is IFeatureLayer)
                {

                    if (player.FeatureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                    {
                        cboPointLayer.Items.Add(player.Name);
                    }
                    if ((player.FeatureClass.ShapeType == esriGeometryType.esriGeometryLine) || (player.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline))
                    {
                        cboLineLayer.Items.Add(player.Name);
                    }
                }

            }
            if ((cboLineLayer.Items.Count == 0) || (cboPointLayer.Items.Count == 0))
            {
                MessageBox.Show("Must have at least one point and one line layer present to continue.", "Draw Lateral Lines", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                this.Close();
            }

        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {

            if ((cboLineLayer.Text.Length == 0) || (cboPointLayer.Text.Length == 0) || (txtbuffer.Text.Length == 0))
            {
                MessageBox.Show("Line layer, point layer, and buffer must be set to continue.", "Draw Lateral Lines", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            IMxDocument pmxdoc = ArcMap.Document as IMxDocument;
            IMap pmap = pmxdoc.FocusMap;
            IFeatureLayer pPointLayer = FindLayer(pmap, cboPointLayer.Text) as IFeatureLayer;
            IFeatureLayer pLineLayer = FindLayer(pmap, cboLineLayer.Text) as IFeatureLayer;

            MessageBox.Show("start: " + DateTime.Now.ToLongTimeString());
            bool isSucccessful = DrawLateralLines(pPointLayer, pLineLayer);
            MessageBox.Show("end: " + DateTime.Now.ToLongTimeString());
            if (isSucccessful)
            {
                MessageBox.Show("Complete!", "Draw Lateral Lines", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                this.Close();
            }

        }




        private bool DrawLateralLines(IFeatureLayer pPointLayer, IFeatureLayer pLineLayer)
        {

            //IProgressDialog2 pProgressDialog = null;
            IStatusBar _Status = ArcMap.Application.StatusBar;
          


            try
            {
                IFeatureLayer pLateralLines = new FeatureLayerClass();
                pLateralLines.FeatureClass = MakeScratchLineLayer();
                pLateralLines.Name = "Lateral Lines";
                ArcMap.Document.ActiveView.FocusMap.AddLayer(pLateralLines);

                int featCount = pPointLayer.FeatureClass.FeatureCount(null);
                //pProgressDialog = ShowProgressIndicator("Calculating...", featCount, 1);
                //pProgressDialog.ShowDialog();
                IStepProgressor _StepProg = _Status.ProgressBar;
                _StepProg.Position = 1;
                _StepProg.MaxRange = featCount;
                _StepProg.Message = "Processing...";
                _StepProg.StepValue = 1;
                _StepProg.Show();



                IFeatureIndex pFeatureIndex = new FeatureIndexClass();
                pFeatureIndex.FeatureClass = pLineLayer.FeatureClass;
                //IEnvelope pEnvelope = pLineLayer.AreaOfInterest.Envelope;
                //pFeatureIndex.Index(null, pEnvelope);

                IFeatureCursor pFCurOutLines = pLateralLines.Search(null, false);
                pFCurOutLines = pLateralLines.FeatureClass.Insert(true);


                IFeatureBuffer pFBuffer = pLateralLines.FeatureClass.CreateFeatureBuffer();

                //Get a cursor from the point layer
                IFeatureCursor pFCur = pPointLayer.Search(null, false);
                IFeature pPointFeature = pFCur.NextFeature();
                IFeature pLineFeature;

                IFeature pNewLineFeature = null;
                IPointCollection pNewLinePointColl = null;
                IPoint ppointStart = new PointClass();
                IPoint ppointEnd = new PointClass();
                int k = 1;


                IProximityOperator pProximityOperator;
                while (pPointFeature != null)
                {
                    //pProgressDialog.Description = "Processing Point: " + k.ToString() + " of " + featCount.ToString();
                    _StepProg.Message = "Processing Point: " + k.ToString() + " of " + featCount.ToString();

                    System.Diagnostics.Debug.WriteLine("Processing Point: " + k.ToString() + " of " + featCount.ToString());

                    ppointStart = pPointFeature.Shape as IPoint;
                    int iNearestLineOID = GetOIDNearestLine(ppointStart, pLineLayer);
                    if (iNearestLineOID == -1)
                    {
                        int iBuffer = Convert.ToInt32(txtbuffer.Text);
                        MessageBox.Show("Lines are falling outside of the maximum point buffer (" + (iBuffer + (iBuffer * 10)).ToString() + "). Aborting...");
                        //pProgressDialog.HideDialog();
                        ArcMap.Application.StatusBar.ProgressBar.Hide();
                        return false;
                    }
                    pLineFeature = pLineLayer.FeatureClass.GetFeature(iNearestLineOID);

                    pProximityOperator = pLineFeature.ShapeCopy as IProximityOperator;
                    ppointEnd = pProximityOperator.ReturnNearestPoint(ppointStart, 0);



                    //Make the line here
                    pNewLineFeature = pLateralLines.FeatureClass.CreateFeature();
                    pNewLinePointColl = new PolylineClass();

                    object missing = Type.Missing;

                    pNewLinePointColl.AddPoint(ppointStart, ref missing, ref missing);
                    pNewLinePointColl.AddPoint(ppointEnd, ref missing, ref missing);

                    pNewLineFeature.Shape = pNewLinePointColl as PolylineClass;
                    pNewLineFeature.Store();

                    pPointFeature = pFCur.NextFeature();
                    k++;

                    ArcMap.Application.StatusBar.ProgressBar.Step();
                }


                //pFCur.Flush();
                System.Runtime.InteropServices.Marshal.ReleaseComObject(pFCur);


                //pProgressDialog.HideDialog();
                ArcMap.Application.StatusBar.ProgressBar.Hide();

                IMxDocument pmxdoc = ArcMap.Document as IMxDocument;
                pmxdoc.UpdateContents();
                pmxdoc.ActiveView.Refresh();

                return true;
               

            }

            catch (Exception ex)
            {
                //pProgressDialog.HideDialog();
                ArcMap.Application.StatusBar.ProgressBar.Hide();
                return false;
            }

        }


        private int GetOIDNearestLine(IPoint ppoint, IFeatureLayer pLineLayer)
        {
            try
            {
                //IGeometry pGeom = ppoint as IGeometry;
                //ITopologicalOperator topoOperator = pGeom as ITopologicalOperator;
                //int iBuffer = Convert.ToInt32(txtbuffer.Text);
                //IGeometry buffer = topoOperator.Buffer(iBuffer);


                //IFeatureIndex pFeatureIndex = new FeatureIndexClass();
                //pFeatureIndex.FeatureClass = pLineLayer.FeatureClass;
                ////IEnvelope pEnvelope = pLineLayer.AreaOfInterest.Envelope;
                ////IEnvelope pEnvelope = ppoint.Envelope;
                ////pEnvelope.Expand(500, 500, false);

                //ISpatialFilter spatialFilter = new SpatialFilterClass();
                //spatialFilter.Geometry = buffer;
                //spatialFilter.GeometryField = pLineLayer.FeatureClass.ShapeFieldName;
                //spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                //spatialFilter.SubFields = pLineLayer.FeatureClass.OIDFieldName + ",SHAPE";

                //IFeatureCursor featureCursor = pLineLayer.FeatureClass.Search(spatialFilter, true);

                //IFeature pFeature = featureCursor.NextFeature();

                //Dictionary<int, double> dic = new Dictionary<int, double>();
                //IPoint ppointEnd = new PointClass();
                //IProximityOperator pProximityOperator;
                //while (pFeature != null)
                //{
                //    //for (int i = 0; i <= pFeature.Fields.FieldCount - 1; i++)
                //    //{
                //    //    System.Diagnostics.Debug.WriteLine(pFeature.Fields.Field[i].Name);
                //    //}
                //    pProximityOperator = pFeature.ShapeCopy as IProximityOperator;
                //    double dist = pProximityOperator.ReturnDistance(pGeom);
                //    //System.Diagnostics.Debug.WriteLine("dsit= " + dist.ToString());

                //    if (pFeature.HasOID)
                //    {
                //        dic.Add(pFeature.OID, dist);
                //    }
                //    else
                //    {
                //        string h = pFeature.Fields.FindField("OBJECTID").ToString();
                //        string iOID = pFeature.get_Value(pFeature.Fields.FindField("OBJECTID")).ToString();
                //        int g = Convert.ToInt32(iOID);
                //        try
                //        {
                //            dic.Add(g, dist);
                //        }
                //        catch { }
                //    }

                //    pFeature = featureCursor.NextFeature();
                //}
                int iBuffer = Convert.ToInt32(txtbuffer.Text);
                Dictionary<int, double> dic = GetDistances(ppoint, pLineLayer, iBuffer);

                if (dic.Keys.Count == 0)
                {
                    dic.Clear();
                    dic = GetDistances(ppoint, pLineLayer, iBuffer + (iBuffer * 10));
                }
                if (dic.Keys.Count == 0)
                {
                  
                    return -1;
                }

                double minDistance = dic.Values.Min();
                int closestOID = 0;
                foreach (var pair in dic)
                {
                    if (pair.Value == minDistance)
                    {
                        closestOID = pair.Key;
                        break;
                    }

                }


                //System.Diagnostics.Debug.WriteLine("closest= " + closestOID.ToString());
                return closestOID;

                ////pFeatureIndex.Index(null, pEnvelope);

                //IIndexQuery2 pIxQuery = pFeatureIndex as IIndexQuery2;
                //int iNearestLineOID = 0;
                //double pLineDistance = 0;
                //pIxQuery.NearestFeature(pGeom, out iNearestLineOID, out pLineDistance);

                //return iNearestLineOID;

            }

            catch (Exception ex)
            {
                return -1;
            }
        }

        private Dictionary<int, double> GetDistances(IPoint ppoint, IFeatureLayer pLineLayer, int iBuffer)
        {

            //IGeometry pGeom = ppoint as IGeometry;
            //ITopologicalOperator topoOperator = pGeom as ITopologicalOperator;
            ////int iBuffer = Convert.ToInt32(txtbuffer.Text);
            //IGeometry buffer = topoOperator.Buffer(iBuffer);


            //IFeatureIndex pFeatureIndex = new FeatureIndexClass();
            //pFeatureIndex.FeatureClass = pLineLayer.FeatureClass;
            ////IEnvelope pEnvelope = pLineLayer.AreaOfInterest.Envelope;
            ////IEnvelope pEnvelope = ppoint.Envelope;
            ////pEnvelope.Expand(500, 500, false);

            //ISpatialFilter spatialFilter = new SpatialFilterClass();
            //spatialFilter.Geometry = buffer;
            //spatialFilter.GeometryField = pLineLayer.FeatureClass.ShapeFieldName;
            //spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
            //spatialFilter.SubFields = pLineLayer.FeatureClass.OIDFieldName + ",SHAPE";

            //IFeatureCursor featureCursor = pLineLayer.FeatureClass.Search(spatialFilter, true);

            //IFeature pFeature = featureCursor.NextFeature();
            try
            {
                pGeom = ppoint as IGeometry;
                topoOperator = pGeom as ITopologicalOperator;
                //int iBuffer = Convert.ToInt32(txtbuffer.Text);
                buffer = topoOperator.Buffer(iBuffer);


                pFeatureIndex = new FeatureIndexClass();
                pFeatureIndex.FeatureClass = pLineLayer.FeatureClass;
                //IEnvelope pEnvelope = pLineLayer.AreaOfInterest.Envelope;
                //IEnvelope pEnvelope = ppoint.Envelope;
                //pEnvelope.Expand(500, 500, false);

                spatialFilter = new SpatialFilterClass();
                spatialFilter.Geometry = buffer;
                spatialFilter.GeometryField = pLineLayer.FeatureClass.ShapeFieldName;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                spatialFilter.SubFields = pLineLayer.FeatureClass.OIDFieldName + ",SHAPE";

                featureCursor = pLineLayer.FeatureClass.Search(spatialFilter, true);

                pFeature = featureCursor.NextFeature();

                Dictionary<int, double> dic = new Dictionary<int, double>();
                ppointEnd = new PointClass();

                while (pFeature != null)
                {
                    //for (int i = 0; i <= pFeature.Fields.FieldCount - 1; i++)
                    //{
                    //    System.Diagnostics.Debug.WriteLine(pFeature.Fields.Field[i].Name);
                    //}
                    pProximityOperator = pFeature.ShapeCopy as IProximityOperator;
                    double dist = pProximityOperator.ReturnDistance(pGeom);
                    //System.Diagnostics.Debug.WriteLine("dsit= " + dist.ToString());

                    if (pFeature.HasOID)
                    {
                        dic.Add(pFeature.OID, dist);
                    }
                    else
                    {
                        string h = pFeature.Fields.FindField("OBJECTID").ToString();
                        string iOID = pFeature.get_Value(pFeature.Fields.FindField("OBJECTID")).ToString();
                        int g = Convert.ToInt32(iOID);
                        try
                        {
                            dic.Add(g, dist);
                        }
                        catch { }
                    }

                    pFeature = featureCursor.NextFeature();
                }

                //featureCursor.Flush();
                System.Runtime.InteropServices.Marshal.ReleaseComObject(featureCursor);
                return dic;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private IFeatureClass MakeScratchLineLayer()
        {
            IGxCatalogDefaultDatabase Defaultgdb = ArcMap.Application as IGxCatalogDefaultDatabase;
            Type factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.FileGDBWorkspaceFactory");
            IWorkspaceFactory workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance(factoryType);
            IWorkspace pWorkspace = workspaceFactory.OpenFromFile(Defaultgdb.DefaultDatabaseName.PathName, 0);


            IFeatureWorkspace workspace = pWorkspace as IFeatureWorkspace;
            UID CLSID = new UID();
            CLSID.Value = "esriGeodatabase.Feature";

            IFields pFields = new FieldsClass();
            IFieldsEdit pFieldsEdit = pFields as IFieldsEdit;
            pFieldsEdit.FieldCount_2 = 1;


            IGeoDataset geoDataset = ArcMap.Document.ActiveView.FocusMap.get_Layer(0) as IGeoDataset;


            IGeometryDef pGeomDef = new GeometryDef();
            IGeometryDefEdit pGeomDefEdit = pGeomDef as IGeometryDefEdit;
            pGeomDefEdit.GeometryType_2 = esriGeometryType.esriGeometryPolyline;
            pGeomDefEdit.SpatialReference_2 = geoDataset.SpatialReference;



            IField pField;
            IFieldEdit pFieldEdit;

  
            pField = new FieldClass();
            pFieldEdit = pField as IFieldEdit;
            pFieldEdit.AliasName_2 = "SHAPE";
            pFieldEdit.Name_2 = "SHAPE";
            pFieldEdit.Type_2 = esriFieldType.esriFieldTypeGeometry;
            pFieldEdit.GeometryDef_2 = pGeomDef;
            pFieldsEdit.set_Field(0, pFieldEdit);


         

            string strFCName = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetRandomFileName());
            char[] chars = strFCName.ToCharArray();
            if (Char.IsDigit(chars[0]))
            {
                strFCName = strFCName.Remove(0, 1);
            }
            KillExistingFeatureclass(strFCName);


            IFeatureClass pFeatureClass = workspace.CreateFeatureClass("lat_" + strFCName, pFieldsEdit, CLSID, null, esriFeatureType.esriFTSimple, "SHAPE", "");
            return pFeatureClass;



        }



        //private IProgressDialog2 ShowProgressIndicator(string strTitle, int iMax, int iStepValue)
        //{

        //    IProgressDialogFactory pProgressDlgFact;
        //    IProgressDialog2 pProgressDialog;

        //    ITrackCancel pTrackCancel;


        //    //'Show a progress dialog while we cycle through the features 
        //    pTrackCancel = new CancelTrackerClass();
        //    pProgressDlgFact = new ProgressDialogFactoryClass();
        //    pProgressDialog = (IProgressDialog2)pProgressDlgFact.Create(pTrackCancel, 0);
        //    pProgressDialog.CancelEnabled = false;
        //    pProgressDialog.Title = strTitle;
        //    pProgressDialog.Animation = esriProgressAnimationTypes.esriProgressGlobe;


        //    //'Set the properties of the Step Progressor 
        //    pStepProgressor = (IStepProgressor)pProgressDialog;
        //    pStepProgressor.MinRange = 0;
        //    pStepProgressor.MaxRange = iMax;
        //    pStepProgressor.StepValue = iStepValue;

        //    return pProgressDialog;
        //}




        private void KillExistingFeatureclass(string strFilename)
        {
            try
            {

                IGxCatalogDefaultDatabase Defaultgdb = ArcMap.Application as IGxCatalogDefaultDatabase;
                Type factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.FileGDBWorkspaceFactory");
                IWorkspaceFactory workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance(factoryType);
                IWorkspace pWorkspace = workspaceFactory.OpenFromFile(Defaultgdb.DefaultDatabaseName.PathName, 0);

                IFeatureWorkspace pFeatureWorkspace = pWorkspace as IFeatureWorkspace;
                IFeatureLayer pFeatureLayer = new FeatureLayerClass();
                pFeatureLayer.FeatureClass = pFeatureWorkspace.OpenFeatureClass(strFilename);
                IDataset pDataset = pFeatureLayer.FeatureClass as IDataset;
                if (pDataset.CanDelete())
                {
                    pDataset.Delete();
                }
            }

            catch { }
        }


        public ILayer FindLayer(IMap pmap, string layer)
        {
            for (int i = 0; i <= pmap.LayerCount - 1; i++)
            {
                if (pmap.get_Layer(i).Name.ToUpper() == layer.ToUpper())
                    return pmap.get_Layer(i);
            }
            return null;


        }



        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                base.OnPaint(e);

                // System.Drawing.Drawing2D.LinearGradientBrush baseBackground = (New (Point(0, 0)) New (Point(ClientSize.Width, 0)), Color.Gray, Color.RoyalBlue);
                using (Brush b = new
                           System.Drawing.Drawing2D.LinearGradientBrush(new System.Drawing.Point(0, 0), new
                           System.Drawing.Point(this.ClientSize.Width, this.ClientSize.Height),
                           Color.LightGreen, Color.Silver))
                    e.Graphics.FillRectangle(b, ClientRectangle);
            }
            catch { }

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void cboLineLayer_SelectedIndexChanged(object sender, EventArgs e)
        {

        }


   

      

    }
}
