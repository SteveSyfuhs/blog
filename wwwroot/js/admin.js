(function () {

    var setImage = function (img, url) {
        if (!img) {
            return;
        }

        if (!url) {
            img.style.display = 'none';
        }
        else {
            img.style.display = 'block';
            img.src = url;
        }

    };

    var handleImage = function (imgId, urlId) {
        var mediaImg = document.getElementById(imgId);
        var mediaUrl = document.getElementById(urlId);

        if (mediaImg && mediaUrl) {
            setImage(mediaImg, mediaUrl.value);

            mediaUrl.addEventListener('change', function (e) {
                setImage(mediaImg, e.target.value);
            });
        }
    };

    handleImage('media', 'PrimaryMediaUrl');
    handleImage('heroMedia', 'HeroBackgroundImageUrl');

    var substringMatcher = function (strs) {
        return function findMatches(q, cb) {
            var matches;
            matches = [];
            substrRegex = new RegExp(q, 'i');
            $.each(strs, function (i, str) {
                if (substrRegex.test(str)) {
                    matches.push(str);
                }
            });

            cb(matches);
        };
    };

    $('#categories').tagsinput({
        typeaheadjs: ({
            hint: true,
            highlight: true,
            minLength: 1
        }, {
            name: 'categories',
            source: substringMatcher(Blog.Categories)
        })
    });

    var excerpt = document.getElementById("Excerpt");
    var desc = document.getElementById("descCharCount");

    var calculateLength = function (textElement, labelElement) {
        var val = textElement.value;

        var length = val.length;
        var words = val.split(' ').length;

        var indicator = "";

        if (length === 1) {
            indicator = "1 character";
        }
        else {
            indicator = length + " characters / ";
        }

        if (val === '') {
            indicator += "0 words";
        }
        else if (words == 1) {
            indicator += "1 word";
        } else {
            indicator += words + " words";
        }

        labelElement.innerText = indicator;
    };

    calculateLength(excerpt, desc);

    excerpt.addEventListener("input", function (e) {
        calculateLength(e.target, desc);
    });

    var edit = document.getElementById("edit");

    if (edit) {
        // Setup editor
        var editPost = document.getElementById("Content");

        tinymce.init({
            selector: '#Content',
            min_height: 500,
            plugins: [
                'autoresize advlist autolink link image lists charmap print preview hr anchor pagebreak',
                'searchreplace wordcount visualblocks visualchars code fullscreen insertdatetime media nonbreaking',
                'table emoticons template paste help'
            ],
            toolbar: 'undo redo | styleselect | bold italic | alignleft aligncenter alignright alignjustify | ' +
                'bullist numlist outdent indent | link image | print preview media fullpage | ' +
                'forecolor backcolor emoticons',
            menubar: 'file edit view insert format tools table',
            paste_data_images: true,
            image_advtab: true,
            file_picker_types: 'image',
            relative_urls: false,
            convert_urls: false,
            branding: false,
            extended_valid_elements: 'script[src|async|defer|type|charset]',
            images_upload_url: '/edit/upload',
            toolbar_sticky: true,
            images_upload_handler: function (blobInfo, success, failure) {
                var xhr, formData;

                xhr = new XMLHttpRequest();
                xhr.withCredentials = false;
                xhr.open('POST', '/edit/upload');

                xhr.onload = function () {
                    var json;

                    if (xhr.status != 200) {
                        failure('HTTP Error: ' + xhr.status);
                        return;
                    }

                    json = JSON.parse(xhr.responseText);

                    if (!json || typeof json.location != 'string') {
                        failure('Invalid JSON: ' + xhr.responseText);
                        return;
                    }

                    success(json.location);
                };

                formData = new FormData();
                formData.append('file', blobInfo.blob(), blobInfo.filename());
                formData.append('__RequestVerificationToken', document.querySelector("input[name=__RequestVerificationToken]").value);

                xhr.send(formData);
            },
            setup: function (editor) {
                editor.ui.registry.addButton('imageupload', {
                    text: 'image',
                    onAction: function (_) {
                        var fileInput = document.createElement("input");
                        fileInput.type = "file";
                        fileInput.multiple = true;
                        fileInput.accept = "image/*";
                        fileInput.addEventListener("change", handleFileSelect, false);
                        fileInput.click();
                    }
                });
            }
        });

        // Delete post
        var deleteButton = edit.querySelector(".delete");

        if (deleteButton) {
            deleteButton.addEventListener("click", function (e) {
                if (!confirm("Are you sure you want to delete the post?")) {
                    e.preventDefault();
                }
            });
        }

        // File upload
        function handleFileSelect(event) {
            if (window.File && window.FileList && window.FileReader) {

                var files = event.target.files;

                for (var i = 0; i < files.length; i++) {
                    var file = files[i];

                    // Only image uploads supported
                    if (!file.type.match('image'))
                        continue;

                    var reader = new FileReader();
                    reader.addEventListener("load", function (event) {
                        var image = new Image();
                        image.alt = file.name;
                        image.onload = function (e) {
                            image.setAttribute("data-filename", file.name);
                            image.setAttribute("width", image.width);
                            image.setAttribute("height", image.height);
                            tinymce.activeEditor.execCommand('mceInsertContent', false, image.outerHTML);
                        };
                        image.src = this.result;

                    });

                    reader.readAsDataURL(file);
                }

                document.body.removeChild(event.target);
            }
            else {
                console.log("Your browser does not support File API");
            }
        }
    }

    // Delete comments
    var deleteLinks = document.querySelectorAll("#comments a.delete");

    for (var i = 0; i < deleteLinks.length; i++) {
        var link = deleteLinks[i];

        link.addEventListener("click", function (e) {
            if (!confirm("Are you sure you want to delete the comment?")) {
                e.preventDefault();
            }
        });
    }

})();