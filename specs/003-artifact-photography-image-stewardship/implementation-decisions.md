# Feature 003 Implementation Decisions

## T001 - JPEG/PNG Processing Package

**Decision**: Use `SkiaSharp` for Feature 003 image validation and derivative generation in Infrastructure only.

**Package/version considered**:

- `SkiaSharp` `4.151.1`, the current stable NuGet release reviewed on 2026-08-24.
- `SkiaSharp.NativeAssets.Linux` `4.151.1` for Linux development/test runtime support.
- `SixLabors.ImageSharp` `4.0.0` as the originally identified candidate.
- `System.Drawing.Common`, rejected by planning research because modern .NET treats it as Windows-specific.

**Compatibility**:

- `SkiaSharp` `4.151.1` lists `net10.0` compatibility on NuGet.
- The project targets `net10.0`.
- Windows native assets are transitive from `SkiaSharp`; Linux native assets are added explicitly so WSL/Linux validation can run without making Linux a production requirement.

**JPEG support**:

- SkiaSharp decodes compressed bitmap streams and supports JPEG encoding through `SKEncodedImageFormat.Jpeg`.

**PNG support**:

- SkiaSharp decodes PNG images and supports PNG encoding through `SKEncodedImageFormat.Png`.

**Resizing/thumbnail capability**:

- SkiaSharp provides bitmap/image decode, draw, scale, and encode APIs suitable for bounded thumbnail and preview derivative generation.

**License**:

- `SkiaSharp` and `SkiaSharp.NativeAssets.Linux` are MIT licensed.
- MIT licensing is compatible with the Museum-System's intended institutional deployment because it permits use, copying, modification, distribution, sublicensing, and sale when copyright and permission notices are retained.

**Reason selected over alternatives**:

- Selected over `SixLabors.ImageSharp` because current ImageSharp releases use the Six Labors Split License, where direct closed-source enterprise use can require a commercial license unless the consuming organization qualifies under the license criteria. The museum deployment status cannot be assumed to satisfy those criteria.
- Selected over `System.Drawing.Common` because Feature 003 requires portable behavior and the planning research already rejected Windows-specific imaging.
- Selected over heavier ImageMagick/Magick.NET-style options because Feature 003 only needs JPEG/PNG validation plus thumbnail/preview derivatives, and SkiaSharp provides those capabilities under a simple MIT license.

**Boundary decision**:

- Package-specific types must remain in Infrastructure. Domain and Application models remain package-neutral and do not expose SkiaSharp types.

**Sources reviewed**:

- NuGet `SkiaSharp` 4.151.1 package page: https://www.nuget.org/packages/SkiaSharp/4.151.1
- NuGet `SkiaSharp.NativeAssets.Linux` 4.151.1 package page: https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux/4.151.1
- SkiaSharp MIT license: https://github.com/mono/SkiaSharp/blob/main/LICENSE.md
- SkiaSharp README/platform support: https://github.com/mono/SkiaSharp/blob/main/README.md
- SkiaSharp bitmap decode/encode documentation: https://mono.github.io/SkiaSharp/docs/guides/bitmaps/saving.html
- Six Labors ImageSharp license: https://github.com/SixLabors/ImageSharp/blob/main/LICENSE
