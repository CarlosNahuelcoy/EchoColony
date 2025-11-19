using UnityEngine;
using Verse;

namespace EchoColony
{
    [StaticConstructorOnStartup]
    public static class MyModTextures
    {
        public static readonly Texture2D ChatIcon = ContentFinder<Texture2D>.Get("ChatIcon");
    }
}
