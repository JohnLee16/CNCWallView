using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Newtonsoft.Json;
using Utilities.BimDataProcess;

namespace WallLine3DView
{
    class ReadJSONFile
    {
        public bool ReadJSONFileLocal(string jsonPath, out BimWall bimWall)
        {
            var jsonwall = File.ReadAllText(jsonPath);
            bimWall = JsonConvert.DeserializeObject<BimWall>(jsonwall);
            return true;
        }
    }
}
