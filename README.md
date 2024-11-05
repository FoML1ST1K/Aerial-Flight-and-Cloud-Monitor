# Aerial Flight and Cloud Monitor
This project is a Windows Forms application in C# that visualizes the simulated path of an airplane on a GMap.NET map. The application also generates cloud points on the map, allowing real-time interaction by displaying their positions relative to the airplane. Additional elements include dynamic charts and overlays to monitor various parameters like cloud distance and relative bearing.

## Features
- Smooth Aircraft Movement: The application interpolates angles for smoother rotation of the airplane icon as it moves between waypoints.
- Dynamic Cloud Generation: Clouds are generated randomly across the map or along the airplane's path with specified bearing and distance. The clouds are represented as translucent circles on the map.
- Distance and Angle Measurement: The app calculates distances and angles between the airplane and nearby clouds, displaying this data in real-time.
- Interactive Map Overlays:
    - Flight Points: Markers are periodically added to indicate the airplane's recent positions.
    - Cloud Overlays: Circles representing cloud clusters are generated either randomly or based on the airplane’s current path.
- Data Visualization:
    - Live Charts: The application features three dynamically updating charts that monitor:
        - Distance to Clouds: Displays the calculated distance from the airplane to clouds.
        - Relative Bearing: Shows the bearing between the airplane and clouds.
        - Cloud Angle Relative to Plane: Shows the angle of clouds relative to the airplane's orientation.
- Adjustable Time Axis: Each chart automatically updates its time axis to reflect the last minute of data.

## Key Classes and Methods
- Plane Interpolation: InterpolateAngle smooths the angle changes for realistic airplane turning animations.
- Map and Overlay Management: Methods like AddRandomClouds and AddCloudOnPlanePath manage cloud generation and placement on the map.
- GMap.NET Integration: RotateImage handles image rotation, and GenerateCirclePerimeterPoints generates coordinates for drawing circles around cloud points.
- Timer Control: The timer1_Tick method handles periodic data updates for charts, ensuring real-time visualization of changing conditions.

## Dependencies
- GMap.NET: Used for map and geographic calculations.
- System.Windows.Forms.DataVisualization: Provides chart controls for data plotting.

## Getting Started
- Clone the repository.
- Install Dependencies (e.g., GMap.NET and Windows Forms).
- Run the Application: Start the project in Visual Studio or via the command line.

## Customization
- Cloud Settings: Adjust AddRandomClouds and AddCloudOnPlanePath methods for different cloud placement patterns.
- Chart Settings: Update chart intervals, colors, or ranges to reflect other visual needs.
