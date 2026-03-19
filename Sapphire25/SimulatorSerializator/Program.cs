using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using MemoryPack;
using SimulatorSerializator;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

string contentRoot = @"C:\MSTS";
string outputFolder = Path.Combine(contentRoot, "TourmalineSerial");
string outputRouteFolder = Path.Combine(outputFolder, "Routes");
string outputTrainsFolder = Path.Combine(outputFolder, "Trains");
string outputVehiclesFolder = Path.Combine(outputFolder, "Vehicles");

Directory.CreateDirectory(outputFolder);
FolderModel auxFolderModel = new FolderModel("MSTS", contentRoot, null);

RouteSerializator auxRouter = new RouteSerializator(outputRouteFolder, contentRoot);
await auxRouter.Execute();
ConsistSerializator auxConsist = new ConsistSerializator(outputTrainsFolder, contentRoot);
await auxConsist.Execute();
VehicleSerializator auxVehicles = new VehicleSerializator(outputVehiclesFolder, contentRoot);
await auxVehicles.Execute();
TrackSectionSerializator auxTrackSection = new TrackSectionSerializator(outputFolder, contentRoot);
await auxTrackSection.Execute();

