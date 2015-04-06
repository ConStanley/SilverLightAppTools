using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;
using ESRI.ArcGIS.Client.Symbols;
using ESRI.ArcGIS.Client.Tasks;
using System.Windows.Controls;

namespace SpatialLineQuery.AddIns
{
    [Export(typeof(ICommand))]
    [DisplayNameAttribute("Spatially Line Query Feature Layer")]
    public class SpatialLineQueryTool : IToggleCommand
    {
        private Draw drawObject;
        private bool isChecked;
        private Draw drawline;
        public SpatialLineQueryTool() 
        {
            isChecked = false;

            // Initialize the draw object that will be used to draw the query box
            //drawObject = new Draw(MapApplication.Current.Map)
            //{
            //    LineSymbol = new LineSymbol() { Color = new SolidColorBrush(Colors.Red), Width = 2 },
            //    FillSymbol = new FillSymbol()
            //    {
            //        Fill = new SolidColorBrush(Color.FromArgb(125, 255, 0, 0)),
            //        BorderBrush = new SolidColorBrush(Colors.Red)
            //    },
            //    DrawMode = DrawMode.Rectangle,
            //    IsEnabled = false
            //};
            //drawObject.DrawComplete += drawObject_DrawComplete;
            drawObject = new Draw(MapApplication.Current.Map)
            {
                LineSymbol = new LineSymbol() { Color = new SolidColorBrush(Colors.Red), Width = 2 },
                                FillSymbol = new FillSymbol()
                {
                    Fill = new SolidColorBrush(Color.FromArgb(125, 255, 0, 0)),
                    BorderBrush = new SolidColorBrush(Colors.Red)
                },
                DrawMode = DrawMode.Polyline,
                IsEnabled = true
            };
            drawObject.DrawComplete += drawObject_DrawComplete;
        }

        #region IToggleCommand Members

        public bool CanExecute(object parameter)
        {
            if (MapApplication.Current.SelectedLayer is FeatureLayer)
                return true;
            else
                return false;
        }

        public void Execute(object parameter)
        {
            // Toggle the checked state of the command
            isChecked = !isChecked;
            OnCanExecuteChanged();

            // Enable/disable the draw object based on the checked state
            drawObject.IsEnabled = isChecked;

            if (isChecked)
            {
                // Use ShowWindow instead of MessageBox.  There is a bug with
                // Firefox 3.6 that crashes Silverlight when using MessageBox.Show.
                MapApplication.Current.ShowWindow("Line Query Features", new TextBlock()
                {
                    Text = "Draw a line to query features within the selected layer",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(30),
                    MaxWidth = 480
                });
            }
        }

        public bool IsChecked()
        {
            return isChecked;
        }

        public event EventHandler CanExecuteChanged;

        #endregion

        protected virtual void OnCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
                CanExecuteChanged(this, EventArgs.Empty);
        }

        private void drawObject_DrawComplete(object sender, DrawEventArgs args)
        {
            // Untoggle the command and disable the draw object
            isChecked = false;
            drawObject.IsEnabled = false;
            OnCanExecuteChanged();

            // Query the selected layer with the user-drawn geometry
            ESRI.ArcGIS.Client.Geometry.Geometry geometry = args.Geometry;
            QueryData(geometry);
        }

        private void QueryData(ESRI.ArcGIS.Client.Geometry.Geometry geometry)
        {
            // Initialize the query
            Query query = new Query() { ReturnGeometry = true };
            query.OutFields.Add("*");
            query.Geometry = geometry;
            query.OutSpatialReference = MapApplication.Current.Map.SpatialReference;
            string serviceURL = null;

            if (MapApplication.Current.SelectedLayer is FeatureLayer)
                serviceURL = ((FeatureLayer)MapApplication.Current.SelectedLayer).Url;

            QueryTask queryTask = new QueryTask(serviceURL);

            // Hook to the query's completed event handlers
            queryTask.ExecuteCompleted += QueryTask_ExecuteCompleted;
            queryTask.Failed += QueryTask_Failed;

            // Execute the query
            queryTask.ExecuteAsync(query);
        }

        private void QueryTask_ExecuteCompleted(object sender, QueryEventArgs args)
        {
            // Check whether any results were found
            FeatureSet featureSet = args.FeatureSet;
            if (featureSet == null || featureSet.Features.Count < 1)
            {
                // Use ShowWindow instead of MessageBox.  There is a bug with
                // Firefox 3.6 that crashes Silverlight when using MessageBox.Show.
                MapApplication.Current.ShowWindow("Error", new TextBlock()
                {
                    Text = "No features returned from query",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(30),
                    MaxWidth = 480
                });
                return;
            }

            // Retrieve or create a graphics layer to use for displaying results
            GraphicsLayer graphicsLayer = null;
            if (featureSet.Features[0].Geometry is ESRI.ArcGIS.Client.Geometry.MapPoint)
                graphicsLayer = GetOrCreateLayer("Point Query Results");
            else if (featureSet.Features[0].Geometry is ESRI.ArcGIS.Client.Geometry.Polyline)
                graphicsLayer = GetOrCreateLayer("Polyline Query Results");
            else if (featureSet.Features[0].Geometry is ESRI.ArcGIS.Client.Geometry.Polygon)
                graphicsLayer = GetOrCreateLayer("Polygon Query Results");

            // Add the results to the graphics layer
            graphicsLayer.ClearGraphics();
            foreach (Graphic feature in featureSet.Features)
                graphicsLayer.Graphics.Add(feature);

            // If the layer has not already been added to the map, add it
            if (MapApplication.Current.Map.Layers[graphicsLayer.ID] == null)
                MapApplication.Current.Map.Layers.Add(graphicsLayer);
        }

        // If a graphics layer with the specified ID already exists in the map, retrieve it.  Otherwise, create it.
        private GraphicsLayer GetOrCreateLayer(string layerId)
        {
            Layer layer = MapApplication.Current.Map.Layers[layerId];
            if (layer != null && layer is GraphicsLayer)
            {
                return layer as GraphicsLayer;
            }
            else
            {
                GraphicsLayer gLayer = new GraphicsLayer() { ID = layerId };
                gLayer.SetValue(MapApplication.LayerNameProperty, layerId);
                return gLayer;
            }
        }

        private void QueryTask_Failed(object sender, TaskFailedEventArgs args)
        {
            // Use ShowWindow instead of MessageBox.  There is a bug with
            // Firefox 3.6 that crashes Silverlight when using MessageBox.Show.
            MapApplication.Current.ShowWindow("Error", new TextBlock()
            {
                Text = "Query failed. The service may not support query or the service is not available.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(30),
                MaxWidth = 480
            });
        }
    }
}

