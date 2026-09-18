using System;
using System.Collections.Generic;
using System.Linq;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class PlatformPublisherFactory : IPlatformPublisherFactory
    {
        private readonly IEnumerable<IVideoUploader> _publishers;

        public PlatformPublisherFactory(IEnumerable<IVideoUploader> publishers)
        {
            _publishers = publishers;
        }

        public IVideoUploader GetPublisher(PlatformType platform)
        {
            var publisher = _publishers.FirstOrDefault(u => u.Platform == platform);
            if (publisher == null)
            {
                throw new NotSupportedException($"Không tìm thấy adapter xuất bản cho nền tảng: {platform}");
            }
            return publisher;
        }
    }
}
