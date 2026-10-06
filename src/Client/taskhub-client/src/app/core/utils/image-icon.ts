const ICON_SIZE = 96;

/**
 * A project icon is either a PrimeIcons name or a custom image stored as a data URL.
 */
export function isImageIcon(icon: string | null | undefined): icon is string {
  return !!icon && icon.startsWith('data:image/');
}

/**
 * Scales and centre-crops an image file to a small square PNG, returned as a data URL.
 */
export async function imageFileToIcon(file: File): Promise<string> {
  const bitmap = await createImageBitmap(file);

  try {
    const canvas = document.createElement('canvas');
    canvas.width = ICON_SIZE;
    canvas.height = ICON_SIZE;

    const context = canvas.getContext('2d');
    if (!context) {
      throw new Error('Canvas is not available.');
    }

    const scale = Math.max(ICON_SIZE / bitmap.width, ICON_SIZE / bitmap.height);
    const width = bitmap.width * scale;
    const height = bitmap.height * scale;
    context.drawImage(bitmap, (ICON_SIZE - width) / 2, (ICON_SIZE - height) / 2, width, height);

    return canvas.toDataURL('image/png');
  } finally {
    bitmap.close();
  }
}
