lệnh build
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

cần xoá các mục liên quan
# 1. Tìm và xóa các file Deep Learning, AI, và CUDA (NVIDIA)
Get-ChildItem -Path . -Recurse -Include nvinfer*.dll, cublas*.dll, cudnn*.dll, nvrtc*.dll, openvino*.dll, halcondl*.dll | Remove-Item -Force

# 2. Tìm và xóa các file của phần mềm HDevelop và giao diện Qt5
Get-ChildItem -Path . -Recurse -Include hdevelop*.exe, Qt5*.dll | Remove-Item -Force

# 3. (Tùy chọn) Tìm và xóa các bản XL (dành cho ảnh siêu lớn không cần thiết)
Get-ChildItem -Path . -Recurse -Include *xl.dll, *xl.exe | Remove-Item -Force