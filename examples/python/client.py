"""
OpenBioMini Python Client Example
Exemplo de consumo da API REST do OpenBioMini Bridge em Python
"""

import urllib.request
import json
import base64

API_URL = "http://localhost:8080/api"

def check_status():
    req = urllib.request.Request(f"{API_URL}/status")
    with urllib.request.urlopen(req) as resp:
        data = json.loads(resp.read().decode())
        print(f"[*] Status do Leitor: Conectado={data.get('connected')}, Modelo={data.get('model')}, Serial={data.get('serial')}")
        return data

def scan_fingerprint(output_image_path="captura.png"):
    print("[*] Enviando comando de captura... Posicione o dedo no sensor!")
    req = urllib.request.Request(f"{API_URL}/scan", method="POST")
    with urllib.request.urlopen(req) as resp:
        data = json.loads(resp.read().decode())
        if data.get("success"):
            print(f"[✓] Captura realizada com sucesso! Qualidade: {data.get('quality')}%")
            
            # Salva imagem da digital
            img_b64 = data.get("imageBase64")
            if img_b64:
                with open(output_image_path, "wb") as f:
                    f.write(base64.b64decode(img_b64))
                print(f"[✓] Imagem salva em: {output_image_path}")
            
            return data.get("template")
        else:
            print(f"[✗] Erro na captura: {data.get('error')}")
            return None

def verify_match(template_a, template_b):
    payload = json.dumps({"templateA": template_a, "templateB": template_b}).encode("utf-8")
    req = urllib.request.Request(f"{API_URL}/match", data=payload, headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req) as resp:
        data = json.loads(resp.read().decode())
        return data.get("match", False)

if __name__ == "__main__":
    print("=== OpenBioMini Python Client ===")
    status = check_status()
    if status.get("connected"):
        t1 = scan_fingerprint("digital_1.png")
        if t1:
            print(f"Template extraído: {t1[:30]}... (Tamanho total: {len(t1)} caracteres base64)")
    else:
        print("[!] Inicie o OpenBioMini.Bridge.exe antes de rodar o script.")
