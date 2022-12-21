using Renga.Core.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Renga.Core
{
    internal class HRAM : GenericMemoryRW
    {
        public HRAM() : base(0x7F, 0x7F) { }
    }
}
