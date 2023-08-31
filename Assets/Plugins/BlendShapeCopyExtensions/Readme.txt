# インストール
BlendShapeCopyExtensionsをUnityの中に入れれば動くはず。
Consoleに`Ignoring menu item SkinnedMeshRenderer because it is in no submenu!`ってエラーが出るけど動く。

# 使い方
1. コピー元のメッシュを選択し、InspectorのSkinned Mesh Render上で右クリックする。
2. そうすると出てくるメニューの下あたりに`Copy BlendShapes`があるのでそれをクリックする。
3. コピー先のメッシュを選択し、InspectorのSkinned Mesh Render上で右クリックする。
4. そうすると出てくるメニューの下あたりに`Paste BlendShapes ~`と出てくるので選んでクリックする。

`Paste BlendShapes Strictly`と`Paste BlendShapes By Name`がある。
`Paste BlendShapes Strictly`はBlendShapeの名前がコピー先とコピー元で全て一致し、過不足がないことも確認してからコピーする。
`Paste BlendShapes By Name`はBlendShapeの名前が一致するものだけをコピーする、違うモデルのBlendShapeでも名前が同じなら値がコピーされる。

# 追記
2021/08/13
`Paste BlendShapes Only Non-Zero Values`を追加、値が0ではないBlendShapeだけコピーする。
Animation作成のときのために。

# 規約
MIT LICENCE
要約: 著作権表示とMITライセンスの全文の掲載さえすればだいたいOK、使うだけなら何も気にしなくていいけど自己責任。

# GitHub
https://github.com/4hiziri/Unity-BlendShapeCopyExtension/tree/main