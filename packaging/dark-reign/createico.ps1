$imgMagick = "& magick.exe"
$env:Path = $env:Path + ';C:\Program Files\ImageMagick-7.0.9-Q16'
$image = "mod_scalable_yellow.svg"

Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -define icon:auto-resize out\dr.ico"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 16x16 out\dr_16x16.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 24x24 out\dr_24x24.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 32x32 out\dr_32x32.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 48x48 out\dr_48x48.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 64x64 out\dr_64x64.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 96x96 out\dr_96x96.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 100x100 out\dr_100x100.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 128x128 out\dr_128x128.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 256x256 out\dr_256x256.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 512x512 out\dr_512x512.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 768x768 out\dr_768x768.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 1024x1024 out\dr_1024x1024.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 1024 $image -resize 252x252 out\banner_252x252.png"
Invoke-Expression "$imgMagick convert -transparent white -background None -density 384 $image -resize 1280x640 out\github_socialmedia_1280x640.png"
Invoke-Expression "$imgMagick convert -transparent black -background None -density 1024 $image -resize 256x256 out\logo-ingame.png"