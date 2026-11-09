// Saves a server-generated stream as a file in the user's browser. Nothing is stored on the server.
window.downloadFileFromStream = async (fileName, contentType, streamReference) => {
  const buffer = await streamReference.arrayBuffer();
  const url = URL.createObjectURL(new Blob([buffer], { type: contentType }));
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
};
