function loadImage(dataURI, canvasElement, maxImageWidth, maxImageHeight, dotNetHelper) {

    var image = new Image();
    var reader = new FileReader();

    fetch(dataURI).
        then((response) => response.blob())
        .then((blob) => {
            reader.onload = () => {
                image.onload = () => {
                    var canvas = document.getElementById(canvasElement);
                    if (image.width > maxImageWidth || image.height > maxImageHeight) {
                        if (image.width > image.height) {
                            let width = maxImageWidth
                            let height = parseInt(image.height * (maxImageWidth / image.width));
                            canvas.width = width;
                            canvas.height = height;
                        }
                        else {
                            let height = maxImageHeight;
                            let width = parseInt(image.width * (maxImageHeight / image.height));
                            canvas.width = width;
                            canvas.height = height;
                        }
                    }
                    else {
                        canvas.width = image.width;
                        canvas.height = image.height;
                    }
                    canvas.getContext('2d').drawImage(image, 0, 0, canvas.width, canvas.height);
                    var data = canvas.toDataURL('image/png');
                    dotNetHelper.invokeMethodAsync("SetDataURL", data);
                }
                image.src = reader.result;
            }
            reader.readAsDataURL(blob);

        });

}

