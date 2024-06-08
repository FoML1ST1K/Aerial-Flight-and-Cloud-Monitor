using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using GMap.NET.WindowsForms.ToolTips;

namespace Diplo
{
    public partial class Form : System.Windows.Forms.Form
    {
        List<PointLatLng> cloudCenters = new List<PointLatLng>();
        GMarkerGoogle planeMarker;
        int point_counter = 0;
        Timer timer = new Timer();
        double speed = 0.01; // Скорость перемещения самолета
        bool avoidingCloud = false; // Флаг, указывающий, что самолет обходит облако
        double currentBearing; // Текущий угол движения самолета
        double cloudAngleRelativeToPlane;
        Bitmap originalPlaneBitmap;
        double relativeBearing;
        private double avoidCloudTimeout = 5; // Время гистерезиса в секундах
        private double avoidCloudTimer;
        private bool isAvoidingCloud = false;

        //PointLatLng targetPoint = new PointLatLng(51.5074, -0.1278); // Координаты Лондона
        //PointLatLng targetPoint = new PointLatLng(39.8283, -98.5795); // США
        //PointLatLng targetPoint = new PointLatLng(45.4215, -75.6919); // Оттава
        //PointLatLng targetPoint = new PointLatLng(0, 0); // Оттава
        //PointLatLng targetPoint = new PointLatLng(40.7128, -74.0060); // Координаты Нью-Йорка
        //PointLatLng targetPoint = planeMarker.Position; // Координаты Самолёта
        //PointLatLng targetPoint = new PointLatLng(55.9726, 37.4148); // Москва
        PointLatLng targetPoint = new PointLatLng(59.8000, 30.2622); // Санкт-Петербурга


        double distanceToCloud = 0;

        public Form()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void gMapControl1_Load(object sender, EventArgs e)
        {
            gmap.MapProvider = GMapProviders.GoogleMap;
            GMaps.Instance.Mode = AccessMode.ServerOnly;
            gmap.MinZoom = 2;
            gmap.MaxZoom = 18;
            gmap.Zoom = 8;

            AddRandomClouds(800);

            // Добавляем маркер с фото
            double photoLatitude = 55.9726; // Широта фото
            double photoLongitude = 37.4148; // Долгота фото
            PointLatLng photoLocation = new PointLatLng(photoLatitude, photoLongitude);
            originalPlaneBitmap = new Bitmap(@"C:\Users\listi\Desktop\Папка\Университет\Диплом\Diplom\plane.png");
            planeMarker = new GMarkerGoogle(photoLocation, originalPlaneBitmap);
            AddCloudOnPlanePath(planeMarker.Position, targetPoint);

            // Устанавливаем начальный угол движения самолета
            currentBearing = GMap.NET.MapProviders.EmptyProvider.Instance.Projection.GetBearing(planeMarker.Position, targetPoint);

            // Устанавливаем интервал таймера и добавляем обработчик события Tick
            timer.Interval = 30; // Интервал в миллисекундах
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            double distanceToDestination = GMap.NET.MapProviders.EmptyProvider.Instance.Projection.GetDistance(planeMarker.Position, targetPoint);
            if (distanceToDestination <= 10)
            {
                timer.Stop();
            }
            else
            {
                // Создаем маршрут для точек полета
                Printpoint(planeMarker.Position.Lat, planeMarker.Position.Lng);

                // Удаляет прошлые копии самолёта
                foreach (GMapOverlay overlay in gmap.Overlays)
                {
                    if (overlay.Id == "planeflightOverlay")
                    {
                        gmap.Overlays.Remove(overlay);
                        break;
                    }
                }

                // Добавляем новый Overlay на карту
                GMapOverlay planeflightOverlay = new GMapOverlay("planeflightOverlay");
                planeflightOverlay.Markers.Add(planeMarker);
                gmap.Overlays.Add(planeflightOverlay);

                // Центрируем карту на новой позиции самолета
                gmap.Position = planeMarker.Position;

                double radarRadius = 0.2; // Радиус радара в градусах (широта и долгота)
                AddRadarOverlay(planeMarker.Position, radarRadius);

                // Вычислиние вершин треугольника для определения облаков в зоне радара
                double extendedRadius = radarRadius * 100; // Увеличение размера треугольника
                PointLatLng vertex1 = GetPointAtBearingAndDistance(planeMarker.Position, currentBearing - 30, extendedRadius);
                PointLatLng vertex2 = GetPointAtBearingAndDistance(planeMarker.Position, currentBearing + 30, extendedRadius);
                PointLatLng vertex3 = GetPointAtBearingAndDistance(planeMarker.Position, currentBearing, extendedRadius * 2);

                bool avoidCloud = false;
                foreach (var cloudCenter in cloudCenters)
                {
                    List<PointLatLng> cloudPerimeterPoints = GenerateCirclePerimeterPoints(cloudCenter, 0.3);
                    foreach (var point in cloudPerimeterPoints)
                    {
                        if (IsPointInTriangle(point, vertex1, vertex2, vertex3))
                        {
                            avoidCloud = true;
                            panel1.BackColor = Color.Red;
                            // Вывод расстояния до облака в консоль
                            distanceToCloud = CalculateDistance(planeMarker.Position, cloudCenter);
                            break;
                        }
                    }
                    if (avoidCloud)
                    {
                        break;
                    }
                }

                if (avoidCloud || avoidingCloud)
                {
                    avoidCloudTimer = avoidCloudTimeout; // Сброс таймера гистерезиса
                    avoidingCloud = true;
                    MovePlane(true);

                    foreach (var cloudCenter in cloudCenters)
                    {
                        double bearing = GMap.NET.MapProviders.EmptyProvider.Instance.Projection.GetBearing(planeMarker.Position, targetPoint); // текущий курс самолета
                        double cloudBearing = GMap.NET.MapProviders.EmptyProvider.Instance.Projection.GetBearing(planeMarker.Position, cloudCenter); // направление от самолета к облаку

                        cloudAngleRelativeToPlane = (cloudBearing - bearing) % 360;
                        if (cloudAngleRelativeToPlane < 0)
                        {
                            cloudAngleRelativeToPlane += 360; // Убедимся, что угол в пределах от 0 до 360
                        }
                        // Выводим угол в консоль или куда-то еще
                        //Console.WriteLine($"Угол облака относительно самолета: {cloudAngleRelativeToPlane} градусов");
                    }

                    // Проверка, вышел ли самолет за пределы облака
                    if (!IsPointInCloud(planeMarker.Position, cloudCenters, 0.3)) // Проверка на наличие облака поблизости
                    {
                        avoidingCloud = false;
                    }
                }
                else
                {
                    panel1.BackColor = Color.Green;
                    MovePlane(false);
                }

                /*            if (isAvoidingCloud == true)
                            {

                                //avoidCloudTimer -= timer.Interval / 1000.0; // Уменьшение таймера гистерезиса
                                if (avoidCloudTimer <= 0)
                                {
                                    Console.WriteLine(avoidCloudTimer);
                                    isAvoidingCloud = false; // Выключение режима "облет облака"
                                    panel1.BackColor = Color.Green;
                                }
                            }*/
            }
        }

        private bool IsPointInCloud(PointLatLng point, List<PointLatLng> cloudCenters, double cloudRadius)
        {
            foreach (var cloudCenter in cloudCenters)
            {
                double distanceToCloudCenter = CalculateDistance(point, cloudCenter);
                if (distanceToCloudCenter <= cloudRadius)
                {
                    return true;
                }
            }
            return false;
        }

        private void AddRadarOverlay(PointLatLng center, double radius)
        {
            // Удалите существующее наложение радара, если оно присутствует
            for (int i = gmap.Overlays.Count - 1; i >= 0; i--)
            {
                if (gmap.Overlays[i].Id == "radarOverlay")
                {
                    gmap.Overlays.RemoveAt(i);
                    break;
                }
            }

            GMapOverlay radarOverlay = new GMapOverlay("radarOverlay");

            // Используем текущий угол направления самолета
            double planeBearing = currentBearing;

            // Вычисление вершин треугольника
            double extendedRadius = radius * 100; // Увеличить размер треугольника

            // Первые две вершины перед плоскостью
            PointLatLng vertex1 = GetPointAtBearingAndDistance(center, planeBearing - 30, extendedRadius * 2);
            PointLatLng vertex2 = GetPointAtBearingAndDistance(center, planeBearing + 30, extendedRadius * 2);

            // Третья вершина в положении плоскости, скорректированная вверх
            double verticalOffset = 0.05; // Настройка поднятия вершины
            PointLatLng vertex3 = new PointLatLng(center.Lat + verticalOffset, center.Lng);

            // Создание треугольника (радара)
            List<PointLatLng> trianglePoints = new List<PointLatLng> { vertex1, vertex2, vertex3 };
            GMapPolygon radarTriangle = new GMapPolygon(trianglePoints, "radarTriangle")
            {
                Fill = new SolidBrush(Color.FromArgb(50, Color.Blue)), // Полупрозрачный синий
                Stroke = new Pen(Color.Blue, 1) // Синий контур
            };
            radarOverlay.Polygons.Add(radarTriangle);

            // Добавьте наложение на карту
            gmap.Overlays.Add(radarOverlay);
        }

        private PointLatLng GetPointAtBearingAndDistance(PointLatLng startPoint, double bearing, double distance)
        {
            double radiusEarthKilometers = 6371.0; // Радиус Земли в километрах

            double lat = startPoint.Lat * Math.PI / 180.0; // Преобразование широты в радианы
            double lng = startPoint.Lng * Math.PI / 180.0; // Преобразование долготы в радианы
            double bearingRad = bearing * Math.PI / 180.0; // Перевести пеленг в радианы

            double newLat = Math.Asin(Math.Sin(lat) * Math.Cos(distance / radiusEarthKilometers) +
                            Math.Cos(lat) * Math.Sin(distance / radiusEarthKilometers) * Math.Cos(bearingRad));
            double newLng = lng + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(distance / radiusEarthKilometers) * Math.Cos(lat),
                                              Math.Cos(distance / radiusEarthKilometers) - Math.Sin(lat) * Math.Sin(newLat));

            return new PointLatLng(newLat * 180.0 / Math.PI, newLng * 180.0 / Math.PI); // Преобразование радианов обратно в градусы
        }

        private bool IsPointInTriangle(PointLatLng p, PointLatLng p0, PointLatLng p1, PointLatLng p2) // Находится ли облако в площади треугольника
        {
            double a = ((p1.Lng - p2.Lng) * (p.Lat - p2.Lat) + (p2.Lat - p1.Lat) * (p.Lng - p2.Lng)) /
                          ((p1.Lng - p2.Lng) * (p0.Lat - p2.Lat) + (p2.Lat - p1.Lat) * (p0.Lng - p2.Lng));
            double b = ((p2.Lng - p0.Lng) * (p.Lat - p2.Lat) + (p0.Lat - p2.Lat) * (p.Lng - p2.Lng)) /
                          ((p2.Lng - p0.Lng) * (p1.Lat - p2.Lat) + (p0.Lat - p2.Lat) * (p1.Lng - p2.Lng));
            double c = 1 - a - b;

            return a >= 0 && b >= 0 && c >= 0;
        }

        private double CalculateDistance(PointLatLng point1, PointLatLng point2)
        {
            double R = 6371.0; // Радиус Земли в километрах
            double lat1 = point1.Lat * Math.PI / 180.0;
            double lon1 = point1.Lng * Math.PI / 180.0;
            double lat2 = point2.Lat * Math.PI / 180.0;
            double lon2 = point2.Lng * Math.PI / 180.0;
            double dlat = lat2 - lat1;
            double dlon = lon2 - lon1;
            double a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dlon / 2) * Math.Sin(dlon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distance = R * c;
            return distance;
        }

        private void MovePlane(bool avoidCloud)
        {
            PointLatLng newPosition;
            if (avoidCloud)
            {
                // Движение к целевой точкес поворотом
                //double angleOffset = 30; // Угол отклонения для обхода облака вправо
                double angleOffset = 30; // Угол отклонения для обхода облака влево
                double avoidanceBearing = (currentBearing + angleOffset) % 360;

                currentBearing = InterpolateAngle(currentBearing, avoidanceBearing, 0.1); // Плавное изменение угла
                newPosition = GetPointAtBearingAndDistance(planeMarker.Position, currentBearing, speed * 150);

                // Плавное возвращение к целевой точке после первого поворота
                /*if (relativeBearing < 0) relativeBearing += 360;
                if (relativeBearing > 30 && relativeBearing < 330)
                {
                    double correctionAngle = -90; // Поворот налево
                    double correctionBearing = (currentBearing + correctionAngle) % 360;
                    currentBearing = InterpolateAngle(currentBearing, correctionBearing, -6000.1); // Плавное изменение угла
                    newPosition = GetPointAtBearingAndDistance(planeMarker.Position, currentBearing, speed * 150);
                }*/
            }
            else
            {
                // Движение к целевой точке
                double directBearing = GMap.NET.MapProviders.EmptyProvider.Instance.Projection.GetBearing(planeMarker.Position, targetPoint);
                currentBearing = InterpolateAngle(currentBearing, directBearing, 0.1); // Плавное изменение угла
                newPosition = GetPointAtBearingAndDistance(planeMarker.Position, currentBearing, speed * 150);
            }

            // Установка новой позиции самолета
            planeMarker.Position = newPosition;

            // Поворачиваем изображение самолета
            planeMarker.Bitmap = RotateImage(originalPlaneBitmap, (float)currentBearing);

            // Рассчитываем относительный угол поворота относительно цели
            double targetBearing = GMap.NET.MapProviders.EmptyProvider.Instance.Projection.GetBearing(planeMarker.Position, targetPoint);
            relativeBearing = currentBearing - targetBearing;
            if (relativeBearing < 0) relativeBearing += 360;

            // Выводим относительный угол поворота в консоль
            //Console.WriteLine($"Relative Bearing: {relativeBearing}");
        }

        private double InterpolateAngle(double current, double target, double factor) // Вычисление плавного поворота
        {
            double difference = target - current;
            if (difference > 180)
            {
                difference -= 360;
            }
            else if (difference < -180)
            {
                difference += 360;
                //difference -= 360;
            }

            return current + difference * (factor / 2);
        }

        private Bitmap RotateImage(Bitmap bmp, float angle)
        {
            double height = bmp.Height;
            double width = bmp.Width;
            int hypotenuse = System.Convert.ToInt32(System.Math.Floor(Math.Sqrt(height * height + width * width)));
            Bitmap rotatedImage = new Bitmap(hypotenuse, hypotenuse);
            using (Graphics g = Graphics.FromImage(rotatedImage))
            {
                g.TranslateTransform((float)(rotatedImage.Width / 2), rotatedImage.Height / 2); // Установить точку вращения как центр в матрице
                g.RotateTransform((float)angle); //rotate
                g.TranslateTransform((float)(-rotatedImage.Width / 3), -rotatedImage.Height / 2); // Восстановить точку вращения в матрице
                g.DrawImage(bmp, (float)((hypotenuse - width) / 100), (float)((hypotenuse - height) / 2), (float)width, (float)height);
            }
            return rotatedImage;
        }

        private void Printpoint(double newLatitude, double newLongitude)
        {
            List<GMapMarker> flightpointMarkers = new List<GMapMarker>();
            GMapOverlay flightpointOverlay = new GMapOverlay("flightpointOverlay");

            // Создаем маркер для текущего положения самолета
            point_counter += 1;
            if (point_counter == 20)
            {
                Bitmap ponit = new Bitmap(@"C:\Users\listi\Desktop\Папка\Университет\Диплом\Diplom\tochka.png");
                GMarkerGoogle pointMarker = new GMarkerGoogle(new PointLatLng(newLatitude - 0.04, newLongitude), ponit);
                flightpointMarkers.Add(pointMarker);
                gmap.Overlays.Add(flightpointOverlay);
                point_counter = 0;
            }

            // Добавляет точки полёта на карту
            foreach (var marker in flightpointMarkers)
            {
                flightpointOverlay.Markers.Add(marker);
            }
        }

        private void AddRandomClouds(int numberOfClouds) // создание облаков в рандомной точке на карте
        {
            Random random = new Random();
            var cloudOverlay = new GMapOverlay("cloudOverlay");

            for (int i = 0; i < numberOfClouds; i++)
            {
                double latitude = random.NextDouble() * (90 - (-90)) + (-90); // Генерация случайной широты
                double longitude = random.NextDouble() * (180 - (-180)) + (-180); // Генерация случайной долготы

                // Добавляем координаты центра облака в список
                cloudCenters.Add(new PointLatLng(latitude, longitude));
            }

            gmap.Overlays.Add(cloudOverlay);

            // Вызываем метод AddCirclesAroundPoints, передавая список координат облаков и радиус кругов
            AddCirclesAroundPoints(cloudCenters, 0.3); // Здесь 1000 - примерный радиус круга в метрах
        }

        private void AddCloudOnPlanePath(PointLatLng planePosition, PointLatLng targetPoint)
        {
            var cloudOverlay = new GMapOverlay("cloudOverlay");
            // Рассчитываем направление движения самолета
            double bearing = GMap.NET.MapProviders.EmptyProvider.Instance.Projection.GetBearing(planePosition, targetPoint);

            // Рассчитываем координаты облака вдоль маршрута самолета
            double cloudDistanceFromPlane = 400; // Расстояние от самолета до облака (в градусах)
            //PointLatLng cloudCenter = GetPointAtBearingAndDistance(planePosition, bearing + 3, cloudDistanceFromPlane);
            //PointLatLng cloudCenter1 = GetPointAtBearingAndDistance(planePosition, bearing + 7, cloudDistanceFromPlane - 200);
            PointLatLng cloudCenter = GetPointAtBearingAndDistance(planePosition, bearing, cloudDistanceFromPlane);
            PointLatLng cloudCenter1 = GetPointAtBearingAndDistance(planePosition, bearing, cloudDistanceFromPlane - 200);

            // Добавляем координаты центра облака в список
            cloudCenters.Add(cloudCenter);
            cloudCenters.Add(cloudCenter1);
            gmap.Overlays.Add(cloudOverlay);

            // Добавляем облако на карту
            AddCirclesAroundPoints(new List<PointLatLng> { cloudCenter }, 0.3); // Радиус облака (в градусах)
            AddCirclesAroundPoints(new List<PointLatLng> { cloudCenter1 }, 0.3); // Радиус облака (в градусах)
        }

        private void AddCirclesAroundPoints(List<PointLatLng> points, double radius)
        {
            GMapOverlay circleOverlay = new GMapOverlay("circleOverlay");

            foreach (var point in points)
            {
                List<PointLatLng> perimeterPoints = GenerateCirclePerimeterPoints(point, radius);
                GMapPolygon circlePolygon = new GMapPolygon(perimeterPoints, "circle");
                circlePolygon.Fill = new SolidBrush(Color.FromArgb(50, Color.Red)); // Например, закрасим круг полупрозрачным синим цветом
                circlePolygon.Stroke = new Pen(Color.Red, 1); // Цвет и толщина обводки круга
                circleOverlay.Polygons.Add(circlePolygon);
            }

            gmap.Overlays.Add(circleOverlay);
        }

        private List<PointLatLng> GenerateCirclePerimeterPoints(PointLatLng center, double radius)
        {
            List<PointLatLng> perimeterPoints = new List<PointLatLng>();
            const int pointsCount = 36; // Количество точек на окружности
            const double slice = 2 * Math.PI / pointsCount;

            for (int i = 0; i < pointsCount; i++)
            {
                double angle = slice * i;
                double latitude = center.Lat + Math.Sin(angle) * radius;
                double longitude = center.Lng + Math.Cos(angle) * radius;
                perimeterPoints.Add(new PointLatLng(latitude, longitude));
            }

            return perimeterPoints;
        }

        private int _countSeconds = 0;

        private void Form_Load(object sender, EventArgs e)
        {
            timer1.Enabled = true;

            panel2.BackColor = Color.Blue;
            panel3.BackColor = Color.Red;
            panel1.BackColor = Color.Green;

            chart3.ChartAreas[0].AxisY.Maximum = 100;
            chart3.ChartAreas[0].AxisY.Minimum = 0;

            chart3.ChartAreas[0].AxisX.LabelStyle.Format = "H:mm:ss";
            chart3.Series[0].XValueType = ChartValueType.DateTime;

            chart3.ChartAreas[0].AxisX.Minimum = DateTime.Now.ToOADate();
            //chart3.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddMinutes(1).ToOADate();
            chart3.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddSeconds(60).ToOADate();

            chart3.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Seconds;
            chart3.ChartAreas[0].AxisX.Interval = 5;

            chart2.ChartAreas[0].AxisY.Maximum = 90;
            chart2.ChartAreas[0].AxisY.Interval = 30;
            chart2.ChartAreas[0].AxisY.Minimum = -90;

            chart2.ChartAreas[0].AxisX.LabelStyle.Format = "H:mm:ss";
            chart2.Series[0].XValueType = ChartValueType.DateTime;

            chart2.ChartAreas[0].AxisX.Minimum = DateTime.Now.ToOADate();
            //chart2.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddMinutes(1).ToOADate();
            chart2.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddSeconds(60).ToOADate();

            chart2.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Seconds;
            chart2.ChartAreas[0].AxisX.Interval = 5;

            chart1.ChartAreas[0].AxisY.Maximum = 360;
            chart1.ChartAreas[0].AxisY.Minimum = 0;

            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "H:mm:ss";
            chart1.Series[0].XValueType = ChartValueType.DateTime;

            chart1.ChartAreas[0].AxisX.Minimum = DateTime.Now.ToOADate();
            //chart1.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddMinutes(1).ToOADate();
            chart1.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddSeconds(60).ToOADate();

            chart1.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Seconds;
            chart1.ChartAreas[0].AxisX.Interval = 5;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            DateTime timeNow = DateTime.Now;

            chart3.Series[0].Points.AddXY(timeNow, distanceToCloud);
            distanceToCloud = 0;

            chart2.Series[0].Points.AddXY(timeNow, relativeBearing);
            relativeBearing = 0;

            chart1.Series[0].Points.AddXY(timeNow, cloudAngleRelativeToPlane);
            cloudAngleRelativeToPlane = 0;

            _countSeconds++;
            if (_countSeconds == 240)
            {
                _countSeconds = 0;

                chart3.ChartAreas[0].AxisX.Minimum = DateTime.Now.ToOADate();
                //chart3.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddMinutes(1).ToOADate();
                chart3.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddSeconds(60).ToOADate();

                chart3.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Seconds;
                chart3.ChartAreas[0].AxisX.Interval = 5;

                chart2.ChartAreas[0].AxisX.Minimum = DateTime.Now.ToOADate();
                //chart2.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddMinutes(1).ToOADate();
                chart2.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddSeconds(60).ToOADate();

                chart2.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Seconds;
                chart2.ChartAreas[0].AxisX.Interval = 5;

                chart1.ChartAreas[0].AxisX.Minimum = DateTime.Now.ToOADate();
                //chart1.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddMinutes(1).ToOADate();
                chart1.ChartAreas[0].AxisX.Maximum = DateTime.Now.AddSeconds(60).ToOADate();

                chart1.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Seconds;
                chart1.ChartAreas[0].AxisX.Interval = 5;
            }
        }
    }
}