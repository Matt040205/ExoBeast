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
    /// Linux-specific platform configuration.
    /// </summary>
    public class LinuxConfig : PlatformConfig
    {
        static LinuxConfig()
        {
            RegisterFactory(() => new LinuxConfig());
        }

        public LinuxConfig() : base(PlatformManager.Platform.Linux)
        {
        }
    }
}

#endif
