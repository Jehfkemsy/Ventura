﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Ventura
{
    public static class Constants
    {
        public enum CipherOption
        {
            Aes,
            TwoFish
        }

        public static int BlockKeySize = 256;
    }
}
