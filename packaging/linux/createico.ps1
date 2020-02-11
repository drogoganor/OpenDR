$imgMagick = "& magick.exe"
$env:Path = $env:Path + ';C:\Program Files\ImageMagick-7.0.9-Q16'

Invoke-Expression "$imgMagick convert -transparent white -density 384 mod_scalable.svg -define icon:auto-resize mod.ico"
Invoke-Expression "$imgMagick convert -transparent white -density 384 mod_scalable.svg -resize 16x16 mod_16x16.png"
Invoke-Expression "$imgMagick convert -transparent white -density 384 mod_scalable.svg -resize 32x32 mod_32x32.png"
Invoke-Expression "$imgMagick convert -transparent white -density 384 mod_scalable.svg -resize 48x48 mod_48x48.png"
Invoke-Expression "$imgMagick convert -transparent white -density 384 mod_scalable.svg -resize 64x64 mod_64x64.png"
Invoke-Expression "$imgMagick convert -transparent white -density 384 mod_scalable.svg -resize 128x128 mod_128x128.png"
Invoke-Expression "$imgMagick convert -transparent white -density 384 mod_scalable.svg -resize 256x256 mod_256x256.png"
Invoke-Expression "$imgMagick convert -transparent white -density 1024 mod_scalable.svg -resize 252x252 banner_252x252.png -transparent white"
Invoke-Expression "$imgMagick convert -transparent white -density 384 mod_scalable.svg -resize 1280x640 github_socialmedia_1280x640.png"
Invoke-Expression "$imgMagick convert -transparent black -density 1024 mod_scalable.svg -resize 256x256 logo-ingame.png"
