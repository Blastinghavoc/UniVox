# UniVox
Unity Voxel Engine developed over ~4 months as my final masters project.

This project implements a voxel framework capable of supporting different implementations of meshing algorithms and voxel storage datastructures, with the project goal being to evaluate the performance implications of 3 meshing algorithms and 3 storage structures. The project also aimed to evaluate these in as "realistic" a scenario as possible, and as such implements most of the important features of a voxel game, such as caves, ores, trees, lighting, and procedural world generation.

Due to the fixed duration/deadline, the project still has a lot of room for improvement, both in terms of features and performance, but was nevertheless fit for purpose. Some features that did not fit in the project scope are NPCs and liquid physics/propgation.

A video of the framework in action is available here: https://www.youtube.com/watch?v=HkmrXZ2zpFY

## Directory layout
The Jasp and TestResults folders can be safely ignored, as these only contain the raw performance data gathered and the statistical analysis performed on that data.

The actual Unity project is contained in the UniVox directory. Of particular note are the Univox/Assets/Scripts and UniVox/Assets/Tests folders, as these contain the source code and the unit tests respectively.
