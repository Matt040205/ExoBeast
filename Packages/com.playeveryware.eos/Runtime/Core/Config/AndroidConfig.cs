/*
 * Copyright (c) 2024 PlayEveryWare
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction.
 */

#if !EOS_DISABLE

namespace PlayEveryWare.EpicOnlineServices
{
    /// <summary>
    /// Android-specific platform configuration.
    /// </summary>
    public class AndroidConfig : PlatformConfig
    {
        static AndroidConfig()
        {
            RegisterFactory(() => new AndroidConfig());
        }

        public AndroidConfig() : base(PlatformManager.Platform.Android)
        {
        }
    }
}

#endif
