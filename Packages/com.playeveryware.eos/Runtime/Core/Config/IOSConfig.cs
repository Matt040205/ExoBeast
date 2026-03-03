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
    /// iOS-specific platform configuration.
    /// </summary>
    public class IOSConfig : PlatformConfig
    {
        static IOSConfig()
        {
            RegisterFactory(() => new IOSConfig());
        }

        public IOSConfig() : base(PlatformManager.Platform.iOS)
        {
        }
    }
}

#endif
