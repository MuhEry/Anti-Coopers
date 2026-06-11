using UnityEngine;
using UnityEditor;

public class GridOlusturucu : EditorWindow
{
    // "Araçlar" yerine "Tools" yazıyoruz, böylece Window ve Help sekmelerinin yanına yerleşecek
[MenuItem("Tools/Duplicate Plane 10x10")]
    public static void GridYap()
    {
        // Sahnede o an fareyle tıkladığın (seçtiğin) objeyi bulur
        GameObject seciliObje = Selection.activeGameObject;

        if (seciliObje == null)
        {
            Debug.LogError("Lütfen önce sahneden çoğaltmak istediğiniz Plane objesini seçin!");
            return;
        }

        // Objeleri toplu tutmak için sahnede boş bir kapsayıcı grup oluşturur
        GameObject grup = new GameObject("Harita_Grid_10x10");
        grup.transform.position = seciliObje.transform.position;

        // Objelerin boyutunu otomatik hesaplar (Görselde Scale X: 0.1, Z: 0.1 olduğu için Unity bunu 1 birim görür)
        // Standart Plane boyutu 10 birimdir. Scale 0.1 ile çarpılınca her bir küpün kapladığı alan 1 birim olur.
        float mesafeX = 1.0f; 
        float mesafeZ = 1.0f;

        for (int x = 0; x < 25; x++)
        {
            for (int z = 0; z < 25; z++)
            {
                // Seçtiğin objenin birebir kopyasını (Clone) üretir
                GameObject yeniKup = Instantiate(seciliObje);
                yeniKup.name = $"Zemin_{x}_{z}";

                // Yeni konumu hesaplar (Yan yana dizilim)
                Vector3 yeniPozisyon = seciliObje.transform.position + new Vector3(x * mesafeX, 0, z * mesafeZ);
                yeniKup.transform.position = yeniPozisyon;

                // Hiyerarşide kalabalık yapmasın diye grubun içine atar
                yeniKup.transform.SetParent(grup.transform);

                // Unity'nin geri al (Ctrl+Z) sistemine kaydeder
                Undo.RegisterCreatedObjectUndo(yeniKup, "Grid Olustur");
            }
        }

        Debug.Log("25x25 Grid başarıyla oluşturuldu! Orijinal ilk objeyi sahneden silebilir veya gizleyebilirsiniz.");
    }
}