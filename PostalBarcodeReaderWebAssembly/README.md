# PostalBarcodeReaderWebAssembly

## 概要

* ブラウザ上で動作する郵便局のカスタマバーコードのリーダーになります。
* 画像やカメラで、カスタマバーコードを読み取ります。

## 使用しているライブラリ

* .Net 7
* [OpenCVSharp](https://github.com/shimat/opencvsharp)
  - 画像の加工や読み取り関係で使用
  - WebAssemblyを使用できるOpenCVSharpのNugetパッケージを利用
* [ImageSharp](https://github.com/SixLabors/ImageSharp)
  - 内部的な画像変換で使用

## このプログラムが動いているサイト

* https://postal-cb-reader.sakura.ne.jp/

## 参考にしたもの

* OpenCVSharpのWebAssemblyサンプルを参考に作成しました。
  - [opencvsharp_blazor_sample](https://github.com/shimat/opencvsharp_blazor_sample)

## 作者

* [twitter](https://twitter.com/Em_epsilon)
