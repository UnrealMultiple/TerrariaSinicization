using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnpackTerrariaTextAsset;

public static class TextAssetHelper
{
    public static string GetUContainerExtension(AssetContainer item)
    {
        string ucont = item.Container;
        if (Path.GetFileName(ucont) != Path.GetFileNameWithoutExtension(ucont))
        {
            return Path.GetExtension(ucont);
        }

        return string.Empty;
    }
}
