using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using PZ2.Model;

namespace PZ2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
		int[,] matrix;	//matrica sluzi da bi znali koliko kocki imamo ukupno (koliko se crta jedna na drugu)
		Dictionary<long, Tuple<IPowerEntity, GeometryModel3D>> Entities;	//lista entiteta, kljuc id, vrednost tupla
		Dictionary<GeometryModel3D, LineEntity> Lines;	//lista linija GEOMETRYMODEL -> kocka
		Point start;


		List<Tuple<IPowerEntity, GeometryModel3D>> linkedEntities;	//lista entiteta obojeni zutom bojom

		Tuple<double, double> minPoint = new Tuple<double, double>(19.793909, 45.2325);	//za ignorisanje svega van ovih kordinata
		Tuple<double, double> maxPoint = new Tuple<double, double>(19.894459, 45.277031);

		const int cubeSize = 5;
		const int zoomMax = 5;

		int zoomCurrent = -zoomMax;

		List<GeometryModel3D> InactiveNetwork;	//liste za cuvanje izbrisanih entiteta (pomocu check boxova) da bi se kasnije vratili

		List<GeometryModel3D> deletedNodes;
		List<GeometryModel3D> deletedSwitches;
		List<GeometryModel3D> deletedSubstations;
		

		public MainWindow()
        {
			matrix = new int[200, 300];
			Entities = new Dictionary<long, Tuple<IPowerEntity, GeometryModel3D>>();
			Lines = new Dictionary<GeometryModel3D, LineEntity>();

			linkedEntities = new List<Tuple<IPowerEntity, GeometryModel3D>>();

			InactiveNetwork = new List<GeometryModel3D>();

			deletedNodes = new List<GeometryModel3D>();
			deletedSwitches = new List<GeometryModel3D>();
			deletedSubstations = new List<GeometryModel3D>();

			InitializeComponent();

			ParseXml();
		}

       
		
		private void ParseXml()
        {
			XmlDocument xmlDoc = new XmlDocument();

			xmlDoc.Load("Geographic.xml");

			foreach (XmlNode node in xmlDoc.SelectNodes("/NetworkModel/Substations/SubstationEntity"))
			{
				ParseEntity(node, new SubstationEntity());
            }

			foreach (XmlNode node in xmlDoc.SelectNodes("/NetworkModel/Nodes/NodeEntity"))
			{
				ParseEntity(node, new NodeEntity());
			}

			foreach (XmlNode node in xmlDoc.SelectNodes("/NetworkModel/Switches/SwitchEntity"))
			{
				ParseEntity(node, new SwitchEntity());
			}

			foreach(XmlNode node in xmlDoc.SelectNodes("/NetworkModel/Lines/LineEntity"))
            {

				LineEntity l = ParseLine(node);

				if(!Entities.ContainsKey(l.FirstEnd) || !Entities.ContainsKey(l.SecondEnd))
                {
					continue;
                }

				DrawPolyline(l);
            }
		}

        private void ParseEntity(XmlNode node, IPowerEntity entity)
        {
			double noviX, noviZ;

			entity.Id = long.Parse(node.SelectSingleNode("Id").InnerText);
			entity.Name = node.SelectSingleNode("Name").InnerText;
			entity.X = double.Parse(node.SelectSingleNode("X").InnerText);
			entity.Z = double.Parse(node.SelectSingleNode("Y").InnerText);
			if (entity.GetType() == typeof(SwitchEntity))
				((SwitchEntity)entity).Status = node.SelectSingleNode("Status").InnerText;

			ToLatLon(entity.X, entity.Z, 34, out noviZ, out noviX);

			if (noviZ < minPoint.Item2 || noviZ > maxPoint.Item2 ||
				noviX < minPoint.Item1 || noviX > maxPoint.Item1)
			{
				return;
			}

			entity.X = ScaleCoordinates(minPoint.Item1, maxPoint.Item1, 0, matrix.GetLength(1) - 1, noviX);	//kordinate se skaliraju na velicinu matrice, zato sto cemo u toj matrici cuvati koliko se kockica nalazi u visinu
			entity.Z = ScaleCoordinates(minPoint.Item2, maxPoint.Item2, 0, matrix.GetLength(0) - 1, noviZ);
			entity.Y = matrix[(int)entity.Z, (int)entity.X]++;	//ovo radimo kako bi postigli efekat u kom kocke redjamo jednu na drugu, dakle kad nadjemo koordinace x i z i nadjemo tu poziciju u matrici (niz nizova) povecavamo je za 1 kada tu zelimo da iscrtamo kocku
			entity.Y = entity.Y * (cubeSize) + 1;	//ovde cuvamo visinu svih kocki, (br. kocki)*(visina jedne) + 1 (za razmak izmedju)

			Entities.Add(entity.Id, new Tuple<IPowerEntity, GeometryModel3D>(entity, DrawNode(entity)));
		}


		private GeometryModel3D DrawNode(IPowerEntity entity)
        {
			int viewX = ScaleCoordinates(0, matrix.GetLength(1), -587, 587, entity.X);	//skaliranje sa velicine matrice na velicinu slike
			int viewZ = 0 - ScaleCoordinates(0, matrix.GetLength(0), -387, 387, entity.Z); // - da bi islo od donjeg levog ugla
			
			MeshGeometry3D mesh = new MeshGeometry3D();

			Point3D[] positions = new Point3D[] { new Point3D(viewX - cubeSize/2, entity.Y, viewZ + cubeSize/2), new Point3D(viewX + cubeSize/2, entity.Y, viewZ + cubeSize/2),
				new Point3D(viewX + cubeSize/2, entity.Y, viewZ - cubeSize/2), new Point3D(viewX - cubeSize/2, entity.Y, viewZ - cubeSize/2), new Point3D(viewX - cubeSize/2, entity.Y + cubeSize, viewZ + cubeSize/2),
				new Point3D(viewX + cubeSize/2, entity.Y + cubeSize, viewZ + cubeSize/2), new Point3D(viewX + cubeSize/2, entity.Y + cubeSize, viewZ - cubeSize/2), new Point3D(viewX - cubeSize/2, entity.Y + cubeSize, viewZ - cubeSize/2) };

			int[] vertices = new int[] {0, 3, 1, 3, 2, 1,/**/ 4, 5, 7, 5, 6, 7, /**/ 0, 7, 3, 0, 4, 7,
				/**/ 1, 2, 5, 2, 6, 5,/**/ 0, 1, 4, 1, 5, 4, /**/ 3, 7, 2, 7, 6, 2};

			mesh.Positions = new Point3DCollection(positions);
			mesh.TriangleIndices = new Int32Collection(vertices);

			GeometryModel3D model = new GeometryModel3D(mesh, GetColor(entity));

			models.Children.Add(model);
			
			return model;
        }

		private DiffuseMaterial GetColor(IPowerEntity entity)
        {
			DiffuseMaterial color;

			if (entity.GetType() == typeof(SubstationEntity))
				color = new DiffuseMaterial(Brushes.Azure);
			else if (entity.GetType() == typeof(NodeEntity))
				color = new DiffuseMaterial(Brushes.Bisque);
			else
				color = new DiffuseMaterial(Brushes.Blue);

			return color;
		}

		private int ScaleCoordinates(double oldMin, double oldMax, double newMin, double newMax, double oldValue)	//skaliranje sa jednog range-a na drugi range
		{
			double oldRange = oldMax - oldMin;
			double newRange = newMax - newMin;

			return Convert.ToInt32((((oldValue - oldMin) * newRange) / oldRange) + newMin);
		}

		private LineEntity ParseLine(XmlNode node)
		{
			LineEntity l = new LineEntity();
			l.Id = long.Parse(node.SelectSingleNode("Id").InnerText);
			l.Name = node.SelectSingleNode("Name").InnerText;
			if (node.SelectSingleNode("IsUnderground").InnerText.Equals("true"))
			{
				l.IsUnderground = true;
			}
			else
			{
				l.IsUnderground = false;
			}
			l.R = float.Parse(node.SelectSingleNode("R").InnerText);
			l.ConductorMaterial = node.SelectSingleNode("ConductorMaterial").InnerText;
			l.LineType = node.SelectSingleNode("LineType").InnerText;
			l.ThermalConstantHeat = long.Parse(node.SelectSingleNode("ThermalConstantHeat").InnerText);
			l.FirstEnd = long.Parse(node.SelectSingleNode("FirstEnd").InnerText);
			l.SecondEnd = long.Parse(node.SelectSingleNode("SecondEnd").InnerText);

			foreach (XmlNode vertice in node.ChildNodes[9].ChildNodes) //vertices -> tacke u kojima linije skrecu (TEMENA)
			{
				double noviX, noviZ;

				ToLatLon(double.Parse(vertice.SelectSingleNode("X").InnerText), double.Parse(vertice.SelectSingleNode("Y").InnerText), 34, out noviZ, out noviX);

				Point3D p = new Point3D(noviX, 1, noviZ);

				l.Vertices.Add(p);
			}

			return l;
		}

		private void DrawPolyline(LineEntity l)
		{
			foreach (Point3D p in l.Vertices)
			{
				if (l.Vertices.IndexOf(p) == (l.Vertices.Count - 1))
				{
					break;
				}
				else
				{
					Lines.Add(DrawLine(p, l.Vertices[l.Vertices.IndexOf(p) + 1]), l);	//ta tacka pa sledeca
				}
			}
		}

		private GeometryModel3D DrawLine(Point3D start, Point3D end)	//prva i druga tacka(temena)
        {
			int startX = ScaleCoordinates(minPoint.Item1, maxPoint.Item1, -587, 587, start.X);	//min i max su u opsegu iz xmla treba da ih skaliramo na velicinu nase slike
			int startZ = 0 - ScaleCoordinates(minPoint.Item2, maxPoint.Item2, -387, 387, start.Z);
			Point3D startPoint = new Point3D(startX, start.Y, startZ);	// ovde mi je Y = 1, njega ne skaliram

			int endX = ScaleCoordinates(minPoint.Item1, maxPoint.Item1, -587, 587, end.X);
			int endZ = 0 - ScaleCoordinates(minPoint.Item2, maxPoint.Item2, -387, 387, end.Z);
			Point3D endPoint = new Point3D(endX, end.Y, endZ);

			Vector3D lineDir = endPoint - startPoint;	//pravac(vektor) izmedju prve i druge tacke 
			Vector3D sideDir = Vector3D.CrossProduct(lineDir, new Vector3D(0, 1, 0)); // pomocu dva vektora dobijamo treci koji ce nam biti vektor do sl tacke, znaci imamo Z i Y i dobijemo X
			sideDir.Normalize();	//da duzina vektora bude jedan, kako bi kasnije mnozili njega sa velicinom kojom mi hocemo 

			GeometryModel3D line = new GeometryModel3D();

			MeshGeometry3D mesh = new MeshGeometry3D();

			Point3D[] positions = new Point3D[8];

			positions[0] = startPoint - sideDir * (cubeSize / 2);
			positions[1] = startPoint + sideDir * (cubeSize / 2);
			positions[2] = endPoint - sideDir * (cubeSize / 2);
			positions[3] = endPoint + sideDir * (cubeSize / 2);

			startPoint.Y += cubeSize;
			endPoint.Y += cubeSize;

			positions[4] = startPoint - sideDir * (cubeSize / 2);
			positions[5] = startPoint + sideDir * (cubeSize / 2);
			positions[6] = endPoint - sideDir * (cubeSize / 2);
			positions[7] = endPoint + sideDir * (cubeSize / 2);

			
			int[] vertices = new int[] { 0, 1, 2,/**/ 1, 3, 2,/**/0, 1, 5,/**/5, 4, 0,/**/7, 3, 2,/**/2, 6, 7,/**/3, 7, 5,/**/5, 1, 3,/**/4, 6, 2,/**/2, 0, 4, /**/4, 5, 7,/**/7, 6, 4};

			mesh.Positions = new Point3DCollection(positions);
			mesh.TriangleIndices = new Int32Collection(vertices);
			line.Geometry = mesh;

			line.Material = new DiffuseMaterial(Brushes.Black);

			models.Children.Add(line);

			return line;
        }



		//From UTM to Latitude and longitude in decimal
		private void ToLatLon(double utmX, double utmY, int zoneUTM, out double latitude, out double longitude)
		{
			bool isNorthHemisphere = true;

			var diflat = -0.00066286966871111111111111111111111111;
			var diflon = -0.0003868060578;

			var zone = zoneUTM;
			var c_sa = 6378137.000000;
			var c_sb = 6356752.314245;
			var e2 = Math.Pow((Math.Pow(c_sa, 2) - Math.Pow(c_sb, 2)), 0.5) / c_sb;
			var e2cuadrada = Math.Pow(e2, 2);
			var c = Math.Pow(c_sa, 2) / c_sb;
			var x = utmX - 500000;
			var y = isNorthHemisphere ? utmY : utmY - 10000000;

			var s = ((zone * 6.0) - 183.0);
			var lat = y / (c_sa * 0.9996);
			var v = (c / Math.Pow(1 + (e2cuadrada * Math.Pow(Math.Cos(lat), 2)), 0.5)) * 0.9996;
			var a = x / v;
			var a1 = Math.Sin(2 * lat);
			var a2 = a1 * Math.Pow((Math.Cos(lat)), 2);
			var j2 = lat + (a1 / 2.0);
			var j4 = ((3 * j2) + a2) / 4.0;
			var j6 = ((5 * j4) + Math.Pow(a2 * (Math.Cos(lat)), 2)) / 3.0;
			var alfa = (3.0 / 4.0) * e2cuadrada;
			var beta = (5.0 / 3.0) * Math.Pow(alfa, 2);
			var gama = (35.0 / 27.0) * Math.Pow(alfa, 3);
			var bm = 0.9996 * c * (lat - alfa * j2 + beta * j4 - gama * j6);
			var b = (y - bm) / v;
			var epsi = ((e2cuadrada * Math.Pow(a, 2)) / 2.0) * Math.Pow((Math.Cos(lat)), 2);
			var eps = a * (1 - (epsi / 3.0));
			var nab = (b * (1 - epsi)) + lat;
			var senoheps = (Math.Exp(eps) - Math.Exp(-eps)) / 2.0;
			var delt = Math.Atan(senoheps / (Math.Cos(nab)));
			var tao = Math.Atan(Math.Cos(delt) * Math.Tan(nab));

			longitude = ((delt * (180.0 / Math.PI)) + s) + diflon;
			latitude = ((lat + (1 + e2cuadrada * Math.Pow(Math.Cos(lat), 2) - (3.0 / 2.0) * e2cuadrada * Math.Sin(lat) * Math.Cos(lat) * (tao - lat)) * (tao - lat)) * (180.0 / Math.PI)) + diflat;
		}



        private void mainViewport_MouseRightButtonDown(object sender, MouseButtonEventArgs e)	//SA VEZBI	
        {
			ResetClick();

			System.Windows.Point mouseposition = e.GetPosition(mainViewport);
			Point3D testpoint3D = new Point3D(mouseposition.X, mouseposition.Y, 0);

			PointHitTestParameters pointparams =
					 new PointHitTestParameters(mouseposition);

			VisualTreeHelper.HitTest(mainViewport, null, HTResult, pointparams);
		}

		private HitTestResultBehavior HTResult(System.Windows.Media.HitTestResult rawresult)
		{	
			RayHitTestResult rayResult = rawresult as RayHitTestResult;	//obj koji je kliknut

			if (rayResult != null)	//kliknuto je nesto sto nama treba
			{
				string t = "";

				string title = "";

                if (Lines.ContainsKey((GeometryModel3D)rayResult.ModelHit))	//kliknuta je linija
                {
					LineEntity l = Lines[(GeometryModel3D)rayResult.ModelHit];
					t = "ID: " + l.Id + "\nName: " + l.Name;

					Entities[l.FirstEnd].Item2.Material = new DiffuseMaterial(Brushes.Yellow);	//ovde bojimo u zuto krajeve linije
					Entities[l.SecondEnd].Item2.Material = new DiffuseMaterial(Brushes.Yellow);

					linkedEntities.Add(Entities[l.FirstEnd]);	//dodajemo ih ovu listu, da bi kasnije vratili kakve su bile
					linkedEntities.Add(Entities[l.SecondEnd]);

					title = "Line:\n";
				}
                else
                {
					foreach (KeyValuePair<long, Tuple<IPowerEntity, GeometryModel3D>> cube in Entities)
					{
						if (cube.Value.Item2 == rayResult.ModelHit)
						{
							t = "ID: " + cube.Key + "\nName: " + cube.Value.Item1.Name;
							if (cube.Value.Item1.GetType() == typeof(SwitchEntity))
								t += "\nStatus: " + ((SwitchEntity)cube.Value.Item1).Status;
							
							break;
						}
					}
					title = "Entity:\n";
				}

				if(t != "")
                {
					MessageBox.Show(t, title, MessageBoxButton.OK, MessageBoxImage.Information);
				}
			}

			return HitTestResultBehavior.Stop;
		}

		private void ResetClick()	//svi koji su obojeni u zuto, vraca ih na njihove pocetne boje i prazni ovu listu (koja ima samo dva clana)
        {
            foreach (Tuple<IPowerEntity, GeometryModel3D> node in linkedEntities)
            {
				node.Item2.Material = GetColor(node.Item1);
            }

			linkedEntities.Clear();
		}

        private void mainViewport_MouseWheel(object sender, MouseWheelEventArgs e)	//ZUMIRANJE
        {	//e ima informaciju koliko puta sam zavrtela (u kojoj velicini), to je atrib Delta
			if(zoomCurrent < zoomMax && e.Delta > 0)	//idemo u zumiranje, zoom ide od -5 do 5
            {
				zoomCurrent++;
				camera.Position += camera.LookDirection * e.Delta;	//menja se i pozicija kamere, menja se ugao kamere kad se zumira
				//lookdirection -> vektor (pravac)
			}

			if(zoomCurrent > -zoomMax && e.Delta < 0)	//idemo od zumiranja
            {
				zoomCurrent--;
				camera.Position += camera.LookDirection * e.Delta;
			}

        }

        private void mainViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
			mainViewport.CaptureMouse();	//pamti da drzim levi klik
        }

        private void mainViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
			mainViewport.ReleaseMouseCapture();	//vise ne drzim levi klik
        }

        private void mainViewport_MouseMove(object sender, MouseEventArgs e)	//POMERANJE (SA VEZBI) i ROTACIJA
        {
			Point end = e.GetPosition(mainViewport);	//tacka koju smo pritisnuli kad smo zavrsili sa rotiranjem
			Vector dir = end - start;	//tacka od koje krecemo sa rotiranjem

			if (mainViewport.IsMouseCaptured)	//ako drzim levi klik, treba da pomeram levo desno plocuu
            {
				Vector3D left = Vector3D.CrossProduct(camera.UpDirection, camera.LookDirection);
				
				camera.Position += left * dir.X + camera.UpDirection * dir.Y;

			}

			if(e.MiddleButton == MouseButtonState.Pressed)
            {
				rotateY.Angle += dir.X;	//menjamo ugao pod kojim se ploca rotira po x i y; ugao koji vektor zaklapa sa plocom je ugao pod kojim se rotira	
				rotateX.Angle += dir.Y;

            }

			start = e.GetPosition(mainViewport); //start uvek mora da bude ono sto smo poslednje pritisnuli
		}


		private void checkInactive_Checked(object sender, RoutedEventArgs e)
		{

			foreach (Model3D m in InactiveNetwork)
			{
				models.Children.Add(m);
			}

			InactiveNetwork.Clear();
		}		//CEKIRANJE ZA NEAKTIVNE LINIJE

		private void checkInactive_Unchecked(object sender, RoutedEventArgs e)
		{

			foreach (KeyValuePair<GeometryModel3D, LineEntity> line in Lines)
			{
				if (Entities[line.Value.FirstEnd].Item1.GetType() == typeof(SwitchEntity))
				{
					SwitchEntity sw = (SwitchEntity)Entities[line.Value.FirstEnd].Item1;

					if (sw.Status == "Open")
					{
						InactiveNetwork.Add(line.Key);

						InactiveNetwork.Add(Entities[line.Value.SecondEnd].Item2);

						models.Children.Remove(line.Key);

						models.Children.Remove(Entities[line.Value.SecondEnd].Item2);
					}

				}

			}
		}
		private void checkSwitchColor_Checked(object sender, RoutedEventArgs e)
        {
			foreach(KeyValuePair<long, Tuple<IPowerEntity, GeometryModel3D>> kvp in Entities)
            {
				if(kvp.Value.Item1.GetType() == typeof(SwitchEntity))
                {
					SwitchEntity sw = (SwitchEntity)kvp.Value.Item1;

					if(sw.Status == "Open")
                    {
						GeometryModel3D swModel = kvp.Value.Item2;

						models.Children.Remove(swModel);	//skine s ploce

						swModel.Material = new DiffuseMaterial(Brushes.Green);	//oboji

						models.Children.Add(swModel);	//vrati

					} else
                    {
						GeometryModel3D swModel = kvp.Value.Item2;

						models.Children.Remove(swModel);

						swModel.Material = new DiffuseMaterial(Brushes.Red);

						models.Children.Add(swModel);
					}

                }
            }
        }   //CEKIRANJE ZA SVICEVE

        private void checkSwitchColor_Unchecked(object sender, RoutedEventArgs e)
        {
			foreach (KeyValuePair<long, Tuple<IPowerEntity, GeometryModel3D>> kvp in Entities)
			{
				if (kvp.Value.Item1.GetType() == typeof(SwitchEntity))
				{
					
					
						GeometryModel3D swModel = kvp.Value.Item2;
						
						models.Children.Remove(swModel);

						swModel.Material = new DiffuseMaterial(Brushes.Blue);	//vratimo sve da su plavi

						models.Children.Add(swModel);

				}
			}
		}

        private void checkLineColor_Checked(object sender, RoutedEventArgs e)	//CEKRIANJE ZA BOJU LINIJE ZA OPSEGE
        {
			

			foreach(KeyValuePair<GeometryModel3D, LineEntity> kvp in Lines)
            {
				GeometryModel3D lineModel = kvp.Key;

				models.Children.Remove(lineModel);

				if (kvp.Value.R < 1)
                {
					
					lineModel.Material = new DiffuseMaterial(Brushes.Red);
				}
				else if(kvp.Value.R >= 1 && kvp.Value.R <= 2)
                {
					lineModel.Material = new DiffuseMaterial(Brushes.Orange);
				}
				else
                {
					lineModel.Material = new DiffuseMaterial(Brushes.Yellow);
				}
				models.Children.Add(lineModel);
			}
		}

        private void checkLineColor_Unchecked(object sender, RoutedEventArgs e)
        {
			foreach (KeyValuePair<GeometryModel3D, LineEntity> kvp in Lines)
			{
				GeometryModel3D lineModel = kvp.Key;

				models.Children.Remove(lineModel);

				lineModel.Material = new DiffuseMaterial(Brushes.Black);
				
				models.Children.Add(lineModel);
			}
		}

        private void checkNodes_Checked(object sender, RoutedEventArgs e)
        {
			foreach(GeometryModel3D model in deletedNodes)
            {
				models.Children.Add(model);
            }

			deletedNodes.Clear();
        }

        private void checkNodes_Unchecked(object sender, RoutedEventArgs e)
        {

			foreach (KeyValuePair<long, Tuple<IPowerEntity, GeometryModel3D>> kvp in Entities)
			{
				if(kvp.Value.Item1.GetType() == typeof(NodeEntity))
                {
					deletedNodes.Add(kvp.Value.Item2);
					models.Children.Remove(kvp.Value.Item2);
                }
			}

		}

		private void checkSwitches_Checked(object sender, RoutedEventArgs e)
        {
			foreach (GeometryModel3D model in deletedSwitches)
			{
				models.Children.Add(model);
			}

			deletedSwitches.Clear();
		}

        private void checkSwitches_Unchecked(object sender, RoutedEventArgs e)
        {

			foreach (KeyValuePair<long, Tuple<IPowerEntity, GeometryModel3D>> kvp in Entities)
			{
				if (kvp.Value.Item1.GetType() == typeof(SwitchEntity))
				{
					deletedSwitches.Add(kvp.Value.Item2);
					models.Children.Remove(kvp.Value.Item2);
				}
			}
		}

        private void checkSubstations_Checked(object sender, RoutedEventArgs e)
        {
			foreach (GeometryModel3D model in deletedSubstations)
			{
				models.Children.Add(model);
			}

			deletedSubstations.Clear();
		}

        private void checkSubstations_Unchecked(object sender, RoutedEventArgs e)
        {
			foreach (KeyValuePair<long, Tuple<IPowerEntity, GeometryModel3D>> kvp in Entities)
			{
				if (kvp.Value.Item1.GetType() == typeof(SubstationEntity))
				{
					deletedSubstations.Add(kvp.Value.Item2);
					models.Children.Remove(kvp.Value.Item2);
				}
			}
		}
    }
}
