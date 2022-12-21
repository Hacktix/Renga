using Renga.Core.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Renga.Core
{
    internal class WRAM : GenericMemoryRW
    {
        public WRAM() : base(0x2000, 0x1FFF) { }
    }
}
