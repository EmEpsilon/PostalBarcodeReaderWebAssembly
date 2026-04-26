var videoElement = null;
var useFront = false;
var videoWidth;
var videoHeight;

function changeVideoMode(videoId, dotNetHelper) {
    useFront = !useFront;
    startVideo(videoId, dotNetHelper);
}

function setVideoSize(width, height) {
    videoWidth = width;
    videoHeight = height;
}

function startVideo(videoId, dotNetHelper) {
    stopVideo();

    var facingMode = useFront ? "user" : { exact: "environment" };
    
    var constraints = {
        audio: false, video: { width: videoWidth, height: videoHeight, facingMode: facingMode }
    };

    navigator.mediaDevices.getUserMedia(constraints).then(function (stream) {
        var { width, height } = stream.getTracks()[0].getSettings();
        dotNetHelper.invokeMethodAsync("SetSize", width, height);
        var video = document.getElementById(videoId);
        video.srcObject = stream;
        video.onloadedmetadata = function (e) {
            video.play();
        };
        videoElement = video;
    })
        .catch(function (err) { console.log(err.name + ":" + err.message); });
}

function stopVideo() {
    if (videoElement == null) { return; }

    const stream = videoElement.srcObject;
    const tracks = stream.getTracks();

    tracks.forEach(function (track) {
        track.stop();
    });

    videoElement.srcObject = null;
    videoElement = null;
}

function getVideoFrameForDisplay(canvasElement, dotNetHelper) {
    if (videoElement == null) {
        dotNetHelper.invokeMethodAsync("TrunOffProcessing");
        return null;
    }

    var canvas = document.getElementById(canvasElement);
    var ctx = canvas.getContext('2d');
    ctx.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
    var imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    var rgbaBytes = new Uint8Array(imageData.data.buffer);
    dotNetHelper.invokeMethodAsync("DisplayImageRaw", canvas.width, canvas.height, rgbaBytes);
}

function getVideoFrame(canvasElement, dotNetHelper) {
    if (videoElement == null) {
        dotNetHelper.invokeMethodAsync("TrunOffProcessing");
        return null;
    }

    var canvas = document.getElementById(canvasElement);
    var ctx = canvas.getContext('2d');
    ctx.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
    var imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    var rgbaBytes = new Uint8Array(imageData.data.buffer);
    dotNetHelper.invokeMethodAsync("ProcessImageRaw", canvas.width, canvas.height, rgbaBytes);
}
