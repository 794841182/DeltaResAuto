# ResPilot 网站维护

网站源文件在 `docs/`，入口是 `docs/index.html`。GitHub Pages 直接从这个目录发布，没有构建步骤。

## 发布

```powershell
git add docs
git commit -m "Update site"
git push
```

推送到 `main` 后，GitHub Pages 会自动重新构建。

线上地址：`https://van1sh-ldu.github.io/ResPilot/`

## 本地预览

直接双击 `docs/index.html`，或在目录里起一个静态服务器：

```powershell
cd docs
python -m http.server 8080
```

## 访问统计

使用 CounterAPI，不需要账号：

- 页面浏览：`https://counterapi.com/stats/van1sh-ldu.github.io/view/respilot`
- 安装版下载点击：把最后一段换成 `download/setup`
- 免安装版下载点击：把最后一段换成 `download/portable`

页脚的浏览数由页面自动读取；下载点击只计数，不阻塞下载。

## 注意

- `docs/assets/` 必须和 `index.html` 一起保留，图片/视频都按相对路径引用。
- 不要把 `assets` 里的素材移到别处，否则 GitHub Pages 上会裂图。
- 每次改完先本地刷新预览，再提交。
