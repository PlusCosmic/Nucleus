## LinuxServer.io FFmpeg Docker Image Summary

### Overview
The `linuxserver/ffmpeg` image is a containerized FFmpeg implementation maintained by LinuxServer.io, based on Ubuntu Noble. It's designed for video/audio transcoding with extensive hardware acceleration support and is regularly updated with the latest codecs and libraries.

### Key Features
- **Current FFmpeg Version**: 8.0 (as of August 2025)
- **Multi-architecture support**: x86_64, ARM64 (with multi-arch manifest)
- **Hardware Acceleration**: Extensive support for Intel, AMD, and NVIDIA GPUs
- **Permission Management**: Automatically matches output file permissions to input file owner/group

### Hardware Acceleration Support

**Intel (x86_64):**
- iHD Driver (default): Gen8+ support
- i965 Driver: Gen5+ support (enable with `LIBVA_DRIVER_NAME=i965`)
- VAAPI: Gen5+ (i965) and Gen8+ (iHD)
- QSV: OneVPL dispatcher with automatic runtime switching
  - OneVPL: Gen12+
  - MSDK: Gen8-Gen12

**NVIDIA:**
- NVENC/NVDEC support via `--runtime=nvidia`

**Vulkan (x86_64):**
- Intel: Set `ANV_VIDEO_DECODE=1`
- AMD: Set `RADV_PERFTEST=video_decode`
- NVIDIA: Requires beta Vulkan drivers on host

**ARM64:**
- Recently added libdrm and rkmpp support (June 2025)

### Basic Usage Examples

**Simple transcode:**
```bash
docker run --rm -it \
  -v $(pwd):/config \
  linuxserver/ffmpeg \
  -i /config/input.mkv \
  -c:v libx264 \
  -b:v 4M \
  -vf scale=1280:720 \
  -c:a copy \
  /config/output.mkv
```

**Intel VAAPI hardware acceleration:**
```bash
docker run --rm -it \
  --device=/dev/dri:/dev/dri \
  -v $(pwd):/config \
  linuxserver/ffmpeg \
  -vaapi_device /dev/dri/renderD128 \
  -i /config/input.mkv \
  -c:v h264_vaapi \
  -b:v 4M \
  -vf 'format=nv12|vaapi,hwupload,scale_vaapi=w=1280:h=720' \
  -c:a copy \
  /config/output.mkv
```

**NVIDIA hardware acceleration:**
```bash
docker run --rm -it \
  --runtime=nvidia \
  -v $(pwd):/config \
  linuxserver/ffmpeg \
  -hwaccel nvdec \
  -i /config/input.mkv \
  -c:v h264_nvenc \
  -b:v 4M \
  -vf scale=1280:720 \
  -c:a copy \
  /config/output.mkv
```

**Vulkan (Intel example):**
```bash
docker run --rm -it \
  --device=/dev/dri:/dev/dri \
  -v $(pwd):/config \
  -e ANV_VIDEO_DECODE=1 \
  linuxserver/ffmpeg \
  -init_hw_device "vulkan=vk:0" \
  -hwaccel vulkan \
  -hwaccel_output_format vulkan \
  -i /config/input.mkv \
  -f null - -benchmark
```

### Important Implementation Notes

1. **Volume Mounting**: Use `/config` as the working directory - mount your files there
2. **Device Access**: For hardware acceleration, mount `/dev/dri:/dev/dri`
3. **Permissions**: Files are automatically created with matching permissions to input files
4. **Container Flags**: Use `--rm` (auto-cleanup) and `-it` (interactive) for one-off transcodes
5. **Environment Variables**: Required for certain GPU features (ANV_VIDEO_DECODE, RADV_PERFTEST, LIBVA_DRIVER_NAME)

### Included Codec Libraries
The image includes extensive codec support: AOM, libdav1d, libdovi, SVT-AV1, rav1e, x264, x265, libvpx, opus, fdk-aac, LAME, theora, vorbis, and many more.

### Version Information
The image receives regular updates (approximately monthly) with library bumps and security patches. Check the changelog for the most recent update details.