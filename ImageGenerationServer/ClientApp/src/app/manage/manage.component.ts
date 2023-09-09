import {Component, Inject} from '@angular/core';
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";

@Component({
  selector: 'app-manage',
  templateUrl: './manage.component.html',
  styleUrls: ['./manage.component.css']
})
export class ManageComponent {
  private imageBaseUrl = environment.imagesHost;

  public phrases: PendingVerifyPhrase[] = [];
  public totalPending = 0;
  public selectedImage = new Set<string>();
  public accessKey = "";
  public imageKeySeparator = ':';

  httpErrorHandler = (error: any) => window.alert(JSON.stringify(error));
  constructor(public http: HttpClient, @Inject('BASE_URL') public baseUrl: string) {
    this.http = http;
  }

  load() {
    this.selectedImage.clear();
    this.http.get<number>(this.baseUrl + 'verify/total', {
      headers: {'X-API-KEY':this.accessKey}
    }).subscribe({
      next: value => this.totalPending = value,
      error: this.httpErrorHandler
    })

    this.http.get<PendingVerifyPhrase[]>(this.baseUrl + 'verify/pending', {
      headers: {'X-API-KEY':this.accessKey}
    })
      .subscribe({
        next: result => { this.phrases = result; this.getImages()},
        error: this.httpErrorHandler
      });
  }

  verified() {
    const phraseMap = new Map(this.phrases.map(i => [i.phrase, [] as number[]]));
    this.selectedImage.forEach(s => {
      const key = s.split(this.imageKeySeparator);
      phraseMap.get(key[0])!.push(Number(key[1]));
    });
    const verifiedPhrased = Array.from(phraseMap,
      ([phrase, removeImageIndex]) => ({phrase, removeImageIndex}));
    console.info(`submit verified=${JSON.stringify(verifiedPhrased)}`);

    this.http.post<any>(this.baseUrl + 'verify/verified', verifiedPhrased, {
      headers: {'X-API-KEY':this.accessKey}
    }).subscribe({
      next: _ => this.load(),
      error: this.httpErrorHandler
    });
  }

  imageUrl(phrase: string): string {
    let filename = phrase.toLowerCase();
    filename = filename.replace(/[^a-zA-Z]/g, '-') + '.json';
    const folder = filename.length < 2 ? filename : filename.slice(0, 2);
    const url = `${this.imageBaseUrl}%2F${folder}%2F${filename}?alt=media`;

    console.info(`'${phrase}' imageUrl = ${url}`);
    return url;
  }

  getImages() {
    console.log(`images size: ${this.phrases.length}`);
    this.phrases.forEach(p => {
      this.http.get<ImagesObject>(this.imageUrl(p.phrase)).subscribe({
        next: images => p.base64Images = images.images,
        error: error => console.error(JSON.stringify(error))
      })
    });
  }

  onImageClick(phrase: string, index: number) {
    const key = phrase + this.imageKeySeparator + index;
    console.log(`key=${key}`);
    if (this.selectedImage.has(key))
      this.selectedImage.delete(key);
    else
      this.selectedImage.add(key);
  }
}

interface PendingVerifyPhrase {
  phrase: string;
  base64Images: string[];
}

interface ImagesObject {
  images : string[],
  isVerify : boolean
}
