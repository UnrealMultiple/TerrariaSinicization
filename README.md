# 该工具用于Terraria 移动版汉化功能

## 相关文件夹
初次运行程序会在同级目录下生成`Import` `Exprot`两个文件夹<br>
执行export命令输出的资源文件在`Export`文件夹中<br>
执行import命令程序会将`Import`文件夹中的资源文件替换data.unity3d中原本的资源文件<br>
你应该把导出文件复制一份放到import文件夹对其内容修改后执行import命令

## 命令
```
UnpackTerrariaTextAsset.exe -export <data.unity3d路径>
UnpackTerrariaTextAsset.exe -import <data.unity3d路径> <输出的data.unity3d文件路径>
```
## 注意
请不要更改导出的资源文件的名字，否则无法替换
