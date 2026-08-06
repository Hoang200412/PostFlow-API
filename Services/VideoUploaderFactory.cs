using System;
using System.Collections.Generic;
using System.Linq;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class VideoUploaderFactory
    {
        private readonly IEnumerable<IVideoUploader> _uploaders;

        public VideoUploaderFactory(IEnumerable<IVideoUploader> uploaders)
        {
            _uploaders = uploaders;
        }

        public IVideoUploader GetUploader(PlatformType platform)
        {
            var uploader = _uploaders.FirstOrDefault(u => u.Platform == platform);
            if (uploader == null)
            {
                throw new NotSupportedException($"Không tìm thấy uploader cho nền tảng: {platform}");
            }
            return uploader;
        }
    }
}
